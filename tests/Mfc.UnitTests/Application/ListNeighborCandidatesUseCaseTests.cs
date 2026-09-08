using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Common;
using Mfc.Application.Inventory;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class ListNeighborCandidatesUseCaseTests
{
    private static CreateSiteUseCase CreateSite(FakeAuthorizationBoundary auth, FakeSiteStore sites)
        => new(auth, sites, new FakeIdempotencyStore(), new FakeAuditEventWriter(), new FakeUnitOfWork());

    private static CreateNodeUseCase CreateNode(
        FakeAuthorizationBoundary auth,
        FakeSiteStore sites,
        FakeNodeStore nodes)
        => new(auth, sites, nodes, new FakeIdempotencyStore(), new FakeAuditEventWriter(), new FakeUnitOfWork());

    private static RegisterDeviceUseCase RegisterDevice(
        FakeAuthorizationBoundary auth,
        FakeNodeStore nodes,
        FakeDeviceStore devices)
        => new(auth, nodes, devices, new FakeIdempotencyStore(), new FakeAuditEventWriter(), new FakeUnitOfWork());

    [Fact]
    public void FilterKeepsOnlyMikroTikWithAddressAndDedupsKnownHosts()
    {
        RouterOsNeighborRow[] rows =
        [
            new() { Address = "192.0.2.10", Platform = "MikroTik", Identity = "edge-a" },
            new() { Address = "192.0.2.11", Platform = "Cisco", Identity = "switch" },
            new() { Address = null, Platform = "MikroTik", Identity = "empty" },
            new() { Address = "192.0.2.12", Platform = "mikrotik", Identity = "seed-chr" },
            new() { Address = "192.0.2.10/24", Platform = "MikroTik", Identity = "dup" },
            new() { Address = "192.0.2.99", Platform = "MikroTik", Identity = "known" },
        ];

        IReadOnlyList<NeighborCandidateView> selected = NeighborCandidateFilter.SelectMikroTikCandidates(
            rows,
            seedIdentity: "seed-chr",
            knownManagementHosts: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "192.0.2.99" });

        Assert.Single(selected);
        Assert.Equal("192.0.2.10", selected[0].Address);
        Assert.Equal((ushort)8729, selected[0].SuggestedPort);
        Assert.Equal("edge-a", selected[0].Identity);
    }

    [Fact]
    public void FilterCollapsesSameIdentityAcrossAddressesPreferringSeedSlash24()
    {
        // Lab-shaped MNDP: one CHR advertised on mgmt, LAN, and VRRP VIP.
        RouterOsNeighborRow[] rows =
        [
            new() { Address = "10.255.11.11", Platform = "MikroTik", Identity = "vrrp-a" },
            new() { Address = "10.255.11.20", Platform = "MikroTik", Identity = "vrrp-a" },
            new() { Address = "10.255.10.11", Platform = "MikroTik", Identity = "vrrp-a" },
            new() { Address = "10.255.11.12", Platform = "MikroTik", Identity = "vrrp-b" },
            new() { Address = "10.255.10.12", Platform = "MikroTik", Identity = "vrrp-b" },
            new() { Address = "10.255.11.13", Platform = "MikroTik", Identity = "peer" },
            new() { Address = "10.255.10.13", Platform = "MikroTik", Identity = "peer" },
        ];

        IReadOnlyList<NeighborCandidateView> selected = NeighborCandidateFilter.SelectMikroTikCandidates(
            rows,
            seedIdentity: "seed",
            knownManagementHosts: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "10.255.10.10" },
            seedManagementHost: "10.255.10.10");

        Assert.Equal(3, selected.Count);
        Assert.Equal("vrrp-a", selected[0].Identity);
        Assert.Equal("vrrp-b", selected[1].Identity);
        Assert.Equal("peer", selected[2].Identity);
        Assert.Equal("10.255.10.11", selected[0].Address);
        Assert.Equal("10.255.10.12", selected[1].Address);
        Assert.Equal("10.255.10.13", selected[2].Address);
    }

    [Fact]
    public void FilterDedupsAnonymousRowsByAddressOnly()
    {
        RouterOsNeighborRow[] rows =
        [
            new() { Address = "198.51.100.1", Platform = "MikroTik", Identity = null },
            new() { Address = "198.51.100.1", Platform = "MikroTik", Identity = " " },
            new() { Address = "198.51.100.2", Platform = "MikroTik", Identity = null },
        ];

        IReadOnlyList<NeighborCandidateView> selected = NeighborCandidateFilter.SelectMikroTikCandidates(
            rows,
            seedIdentity: "seed",
            knownManagementHosts: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(2, selected.Count);
        Assert.Equal("198.51.100.1", selected[0].Address);
        Assert.Equal("198.51.100.2", selected[1].Address);
    }

    [Fact]
    public async Task ExecuteReturnsFilteredCandidatesWithoutMutatingRouterOs()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeConnectionProfileReadStore profiles = new();
        FakeRouterOsReadPort routerOs = new()
        {
            NeighborResult = new RouterOsNeighborDiscoveryResult
            {
                SeedIdentity = "seed-chr",
                Rows =
                [
                    new RouterOsNeighborRow
                    {
                        Address = "198.51.100.8",
                        Platform = "MikroTik",
                        Identity = "peer-b",
                        MacAddress = "AA:BB:CC:DD:EE:FF",
                    },
                    new RouterOsNeighborRow
                    {
                        Address = "198.51.100.9",
                        Platform = "Other",
                        Identity = "noise",
                    },
                ],
            },
        };

        SiteView site = (await CreateSite(auth, sites).ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                Code = "LAB01",
                Name = "Lab",
            })).Value!;
        NodeView node = (await CreateNode(auth, sites, nodes).ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                SiteId = site.Id,
                Name = "core",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            })).Value!;
        DeviceView seed = (await RegisterDevice(auth, nodes, devices).ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                NodeId = node.Id,
                DisplayName = "seed",
                ManagementHost = "192.0.2.1",
                Role = DeviceRole.Router,
            })).Value!;

        profiles.ByDevice[seed.Id] = new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(Guid.NewGuid()),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "ca",
        };

        ApplicationResult<NeighborCandidatesView> result =
            await new ListNeighborCandidatesUseCase(auth, devices, profiles, routerOs).ExecuteAsync(
                new ListNeighborCandidatesCommand { Actor = "a", SeedDeviceId = seed.Id });

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RouterOsMutated);
        Assert.False(routerOs.MutatedRouterOs);
        Assert.Equal(1, routerOs.NeighborListCount);
        Assert.Equal("seed-chr", result.Value.SeedIdentity);
        Assert.Single(result.Value.Candidates);
        Assert.Equal("198.51.100.8", result.Value.Candidates[0].Address);
        Assert.Equal("peer-b", result.Value.Candidates[0].Identity);
    }

    [Fact]
    public async Task ExecuteDeniesWithoutDiscoveryRead()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.DiscoveryRead);
        FakeRouterOsReadPort routerOs = new();

        ApplicationResult<NeighborCandidatesView> result =
            await new ListNeighborCandidatesUseCase(
                    auth,
                    new FakeDeviceStore(),
                    new FakeConnectionProfileReadStore(),
                    routerOs)
                .ExecuteAsync(new ListNeighborCandidatesCommand
                {
                    Actor = "guest",
                    SeedDeviceId = Guid.NewGuid(),
                });

        Assert.False(result.IsSuccess);
        Assert.Equal("forbidden", result.Error!.Code);
        Assert.Equal(0, routerOs.NeighborListCount);
    }

    [Fact]
    public async Task ExecuteFailsWhenSeedMissing()
    {
        FakeAuthorizationBoundary auth = new();
        ApplicationResult<NeighborCandidatesView> result =
            await new ListNeighborCandidatesUseCase(
                    auth,
                    new FakeDeviceStore(),
                    new FakeConnectionProfileReadStore(),
                    new FakeRouterOsReadPort())
                .ExecuteAsync(new ListNeighborCandidatesCommand
                {
                    Actor = "a",
                    SeedDeviceId = Guid.NewGuid(),
                });

        Assert.False(result.IsSuccess);
        Assert.Equal("not_found", result.Error!.Code);
    }
}
