using System.Text;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Common;
using Mfc.Application.Inventory;
using Mfc.Application.Models;
using Mfc.Application.Snapshots;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class InventoryUseCaseTests
{
    private static CreateSiteUseCase CreateSite(
        FakeAuthorizationBoundary auth,
        FakeSiteStore sites,
        FakeIdempotencyStore? idempotency = null,
        FakeAuditEventWriter? audit = null)
        => new(auth, sites, idempotency ?? new FakeIdempotencyStore(), audit ?? new FakeAuditEventWriter());

    private static CreateNodeUseCase CreateNode(
        FakeAuthorizationBoundary auth,
        FakeSiteStore sites,
        FakeNodeStore nodes,
        FakeIdempotencyStore? idempotency = null,
        FakeAuditEventWriter? audit = null)
        => new(auth, sites, nodes, idempotency ?? new FakeIdempotencyStore(), audit ?? new FakeAuditEventWriter());

    private static RegisterDeviceUseCase RegisterDevice(
        FakeAuthorizationBoundary auth,
        FakeNodeStore nodes,
        FakeDeviceStore devices,
        FakeIdempotencyStore? idempotency = null,
        FakeAuditEventWriter? audit = null)
        => new(auth, nodes, devices, idempotency ?? new FakeIdempotencyStore(), audit ?? new FakeAuditEventWriter());

    [Fact]
    public async Task CreateSiteAndNodeAndRegisterDevice()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();

        ApplicationResult<SiteView> site = await CreateSite(auth, sites, idempotency, audit).ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                Code = "EDGE01",
                Name = "Edge",
            });
        Assert.True(site.IsSuccess);
        Assert.Equal("EDGE01", site.Value!.Code);

        ApplicationResult<NodeView> node = await CreateNode(auth, sites, nodes, idempotency, audit).ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                SiteId = site.Value.Id,
                Name = "r1",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            });
        Assert.True(node.IsSuccess);

        ApplicationResult<DeviceView> device = await RegisterDevice(auth, nodes, devices, idempotency, audit)
            .ExecuteAsync(
                new RegisterDeviceCommand
                {
                    Actor = "admin",
                    IdempotencyKey = Guid.NewGuid(),
                    NodeId = node.Value!.Id,
                    DisplayName = "r1",
                    ManagementHost = "10.0.0.1",
                    Role = DeviceRole.Router,
                });
        Assert.True(device.IsSuccess);
        Assert.Equal("10.0.0.1", device.Value!.ManagementHost);
        Assert.Null(device.Value.GetType().GetProperty("Password"));
        Assert.Contains(audit.Events, e => e.Action == CreateSiteUseCase.Operation);
    }

    [Fact]
    public async Task ListSitesPaginatesAndRequiresReadPermission()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        CreateSiteUseCase create = CreateSite(auth, sites);
        for (int i = 0; i < 3; i++)
        {
            Assert.True((await create.ExecuteAsync(new CreateSiteCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                Code = $"S{i:00}",
                Name = $"Site {i}",
            })).IsSuccess);
        }

        ListSitesUseCase list = new(auth, sites);
        ApplicationResult<SiteListPageView> page1 = await list.ExecuteAsync(
            new ListSitesQuery { Actor = "admin", Limit = 2 });
        Assert.True(page1.IsSuccess);
        Assert.Equal(2, page1.Value!.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(page1.Value.NextCursor));

        ApplicationResult<SiteListPageView> page2 = await list.ExecuteAsync(
            new ListSitesQuery { Actor = "admin", Limit = 2, Cursor = page1.Value.NextCursor });
        Assert.True(page2.IsSuccess);
        Assert.Single(page2.Value!.Items);

        auth.DeniedPermissions.Add(ApplicationPermissions.InventoryRead);
        ApplicationResult<SiteListPageView> forbidden = await list.ExecuteAsync(
            new ListSitesQuery { Actor = "guest", Limit = 10 });
        Assert.Equal("forbidden", forbidden.Error!.Code);
    }

    [Fact]
    public async Task GetNodeReturnsDevicesAndNotFound()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        SiteView site = (await CreateSite(auth, sites).ExecuteAsync(new CreateSiteCommand
        {
            Actor = "a",
            IdempotencyKey = Guid.NewGuid(),
            Code = "GN01",
            Name = "GetNode",
        })).Value!;
        NodeView node = (await CreateNode(auth, sites, nodes).ExecuteAsync(new CreateNodeCommand
        {
            Actor = "a",
            IdempotencyKey = Guid.NewGuid(),
            SiteId = site.Id,
            Name = "core",
            DeclaredKind = NodeKind.Router,
            DeclaredUplinkMode = DeclaredUplinkMode.One,
        })).Value!;
        DeviceView device = (await RegisterDevice(auth, nodes, devices).ExecuteAsync(new RegisterDeviceCommand
        {
            Actor = "a",
            IdempotencyKey = Guid.NewGuid(),
            NodeId = node.Id,
            DisplayName = "core",
            ManagementHost = "192.0.2.10",
            Role = DeviceRole.Router,
        })).Value!;

        GetNodeUseCase getNode = new(auth, nodes, devices, new FakeSnapshotStore());
        ApplicationResult<NodeDetailsView> details = await getNode.ExecuteAsync(
            new GetNodeQuery { Actor = "a", NodeId = node.Id });
        Assert.True(details.IsSuccess);
        Assert.Equal(node.Id, details.Value!.Node.Id);
        Assert.Single(details.Value.Devices);
        Assert.Equal(device.Id, details.Value.Devices[0].Id);
        Assert.Equal("Unknown", details.Value.Devices[0].Reachability);
        Assert.Null(details.Value.Devices[0].RouterOsVersion);
        Assert.Null(details.Value.Devices[0].Model);
        Assert.Empty(details.Value.Devices[0].VrrpRoleLabels);

        ApplicationResult<NodeDetailsView> missing = await getNode.ExecuteAsync(
            new GetNodeQuery { Actor = "a", NodeId = Guid.NewGuid() });
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task ListNodesPaginatesBySiteAndRequiresReadPermission()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        SiteView site = (await CreateSite(auth, sites).ExecuteAsync(new CreateSiteCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Code = "LN01",
            Name = "ListNodes",
        })).Value!;
        for (int i = 0; i < 3; i++)
        {
            Assert.True((await CreateNode(auth, sites, nodes).ExecuteAsync(new CreateNodeCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                SiteId = site.Id,
                Name = $"n{i:00}",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            })).IsSuccess);
        }

        ListNodesUseCase list = new(auth, sites, nodes);
        ApplicationResult<NodeListPageView> page1 = await list.ExecuteAsync(
            new ListNodesQuery { Actor = "admin", SiteId = site.Id, Limit = 2 });
        Assert.True(page1.IsSuccess);
        Assert.Equal(2, page1.Value!.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(page1.Value.NextCursor));

        ApplicationResult<NodeListPageView> page2 = await list.ExecuteAsync(
            new ListNodesQuery
            {
                Actor = "admin",
                SiteId = site.Id,
                Limit = 2,
                Cursor = page1.Value.NextCursor,
            });
        Assert.True(page2.IsSuccess);
        Assert.Single(page2.Value!.Items);

        ApplicationResult<NodeListPageView> missingSite = await list.ExecuteAsync(
            new ListNodesQuery { Actor = "admin", SiteId = Guid.NewGuid(), Limit = 10 });
        Assert.Equal("not_found", missingSite.Error!.Code);

        auth.DeniedPermissions.Add(ApplicationPermissions.InventoryRead);
        ApplicationResult<NodeListPageView> forbidden = await list.ExecuteAsync(
            new ListNodesQuery { Actor = "guest", SiteId = site.Id, Limit = 10 });
        Assert.Equal("forbidden", forbidden.Error!.Code);
    }

    [Fact]
    public async Task UpdateDeviceHonorsOptimisticConcurrencyAndIdempotency()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeIdempotencyStore idempotency = new();
        SiteView site = (await CreateSite(auth, sites, idempotency).ExecuteAsync(new CreateSiteCommand
        {
            Actor = "a",
            IdempotencyKey = Guid.NewGuid(),
            Code = "UD01",
            Name = "Update",
        })).Value!;
        NodeView node = (await CreateNode(auth, sites, nodes, idempotency).ExecuteAsync(new CreateNodeCommand
        {
            Actor = "a",
            IdempotencyKey = Guid.NewGuid(),
            SiteId = site.Id,
            Name = "n",
            DeclaredKind = NodeKind.Router,
            DeclaredUplinkMode = DeclaredUplinkMode.One,
        })).Value!;
        DeviceView device = (await RegisterDevice(auth, nodes, devices, idempotency).ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                NodeId = node.Id,
                DisplayName = "d",
                ManagementHost = "192.0.2.11",
                Role = DeviceRole.Router,
            })).Value!;

        UpdateDeviceUseCase update = new(auth, devices, idempotency, new FakeAuditEventWriter());
        Guid key = Guid.NewGuid();
        ApplicationResult<DeviceView> first = await update.ExecuteAsync(new UpdateDeviceCommand
        {
            Actor = "a",
            IdempotencyKey = key,
            DeviceId = device.Id,
            ExpectedRowVersion = device.RowVersion,
            DisplayName = "renamed",
        });
        Assert.True(first.IsSuccess);
        Assert.Equal("renamed", first.Value!.DisplayName);

        ApplicationResult<DeviceView> replay = await update.ExecuteAsync(new UpdateDeviceCommand
        {
            Actor = "a",
            IdempotencyKey = key,
            DeviceId = device.Id,
            ExpectedRowVersion = device.RowVersion,
            DisplayName = "renamed",
        });
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value.Id, replay.Value!.Id);
        Assert.Equal(first.Value.RowVersion, replay.Value.RowVersion);

        ApplicationResult<DeviceView> stale = await update.ExecuteAsync(new UpdateDeviceCommand
        {
            Actor = "a",
            IdempotencyKey = Guid.NewGuid(),
            DeviceId = device.Id,
            ExpectedRowVersion = device.RowVersion,
            DisplayName = "stale",
        });
        Assert.Equal("conflict", stale.Error!.Code);
    }

    [Fact]
    public async Task CreateSiteRejectsDuplicateCode()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        CreateSiteUseCase useCase = CreateSite(auth, sites);
        Assert.True((await useCase.ExecuteAsync(new CreateSiteCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Code = "DUP1",
            Name = "One",
        })).IsSuccess);

        ApplicationResult<SiteView> second = await useCase.ExecuteAsync(new CreateSiteCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Code = "DUP1",
            Name = "Two",
        });
        Assert.False(second.IsSuccess);
        Assert.Equal("conflict", second.Error!.Code);
    }

    [Fact]
    public async Task CreateSiteRejectsInvalidCodeAndBlankName()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        CreateSiteUseCase useCase = CreateSite(auth, sites);
        ApplicationResult<SiteView> badCode = await useCase.ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                Code = "bad code!",
                Name = "X",
            });
        Assert.True(badCode.IsFailure);
        Assert.Equal("validation", badCode.Error!.Code);

        ApplicationResult<SiteView> blank = await useCase.ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                Code = "OK01",
                Name = "   ",
            });
        Assert.Equal("validation", blank.Error!.Code);
    }

    [Fact]
    public async Task CreateNodeRejectsMissingSiteDuplicateNameAndInvalidName()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        SiteView site = (await CreateSite(auth, sites).ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                Code = "SITE1",
                Name = "Site",
            })).Value!;
        CreateNodeUseCase useCase = CreateNode(auth, sites, nodes);

        ApplicationResult<NodeView> missingSite = await useCase.ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                SiteId = Guid.NewGuid(),
                Name = "n1",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            });
        Assert.Equal("not_found", missingSite.Error!.Code);

        Assert.True((await useCase.ExecuteAsync(new CreateNodeCommand
        {
            Actor = "a",
            IdempotencyKey = Guid.NewGuid(),
            SiteId = site.Id,
            Name = "dup",
            DeclaredKind = NodeKind.Router,
            DeclaredUplinkMode = DeclaredUplinkMode.One,
        })).IsSuccess);

        ApplicationResult<NodeView> duplicate = await useCase.ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                SiteId = site.Id,
                Name = "dup",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            });
        Assert.Equal("conflict", duplicate.Error!.Code);

        ApplicationResult<NodeView> invalid = await useCase.ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                SiteId = site.Id,
                Name = " ",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            });
        Assert.Equal("validation", invalid.Error!.Code);
    }

    [Fact]
    public async Task RegisterDeviceRejectsMissingNodeAndInvalidEndpoint()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        SiteView site = (await CreateSite(auth, sites).ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                Code = "SITE2",
                Name = "Site",
            })).Value!;
        NodeView node = (await CreateNode(auth, sites, nodes).ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                SiteId = site.Id,
                Name = "n",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            })).Value!;
        RegisterDeviceUseCase useCase = RegisterDevice(auth, nodes, devices);

        ApplicationResult<DeviceView> missing = await useCase.ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                NodeId = Guid.NewGuid(),
                DisplayName = "d",
                ManagementHost = "10.0.0.1",
                Role = DeviceRole.Router,
            });
        Assert.Equal("not_found", missing.Error!.Code);

        ApplicationResult<DeviceView> invalid = await useCase.ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                NodeId = node.Id,
                DisplayName = "d",
                ManagementHost = "not an ip",
                Role = DeviceRole.Router,
            });
        Assert.Equal("validation", invalid.Error!.Code);
    }

    [Fact]
    public async Task UpdateConnectionProfileDelegatesWithoutReturningSecretMaterial()
    {
        FakeAuthorizationBoundary auth = new();
        FakeConnectionProfileService profiles = new();
        FakeIdempotencyStore idempotency = new();
        ApplicationResult<ConnectionProfileView> result =
            await new UpdateConnectionProfileUseCase(auth, profiles, idempotency).ExecuteAsync(
                new UpsertConnectionProfileCommand
                {
                    Actor = "admin",
                    IdempotencyKey = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    Username = "ro",
                    PasswordUtf8 = Encoding.UTF8.GetBytes("secret"),
                    TrustMode = CertificateTrustMode.InternalCa,
                    CaProfileRef = "lab-ca",
                });

        Assert.True(result.IsSuccess);
        Assert.Single(profiles.Upserts);
        Assert.DoesNotContain(
            result.Value!.GetType().GetProperties(),
            p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateConnectionProfileMapsServiceFailures()
    {
        FakeAuthorizationBoundary auth = new();
        FakeConnectionProfileService profiles = new();
        FakeIdempotencyStore idempotency = new();
        UpdateConnectionProfileUseCase useCase = new(auth, profiles, idempotency);
        UpsertConnectionProfileCommand command = new()
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Username = "ro",
            PasswordUtf8 = Encoding.UTF8.GetBytes("secret"),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab-ca",
        };

        profiles.ThrowOnUpsert = new InvalidOperationException("boom");
        ApplicationResult<ConnectionProfileView> failed = await useCase.ExecuteAsync(command);
        Assert.Equal("failed", failed.Error!.Code);

        profiles.ThrowOnUpsert = new ArgumentException("bad arg");
        command = new UpsertConnectionProfileCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            DeviceId = command.DeviceId,
            Username = "ro",
            PasswordUtf8 = Encoding.UTF8.GetBytes("secret"),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab-ca",
        };
        ApplicationResult<ConnectionProfileView> validation = await useCase.ExecuteAsync(command);
        Assert.Equal("validation", validation.Error!.Code);

        auth.DeniedPermissions.Add(ApplicationPermissions.ConnectionProfileWrite);
        ApplicationResult<ConnectionProfileView> forbidden = await useCase.ExecuteAsync(
            new UpsertConnectionProfileCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                Username = "ro",
                PasswordUtf8 = Encoding.UTF8.GetBytes("secret"),
                TrustMode = CertificateTrustMode.InternalCa,
                CaProfileRef = "lab-ca",
            });
        Assert.Equal("forbidden", forbidden.Error!.Code);
    }
}

public sealed class SnapshotUseCaseTests
{
    private static CreateSiteUseCase CreateSite(FakeAuthorizationBoundary auth, FakeSiteStore sites)
        => new(auth, sites, new FakeIdempotencyStore(), new FakeAuditEventWriter());

    private static CreateNodeUseCase CreateNode(
        FakeAuthorizationBoundary auth,
        FakeSiteStore sites,
        FakeNodeStore nodes)
        => new(auth, sites, nodes, new FakeIdempotencyStore(), new FakeAuditEventWriter());

    private static RegisterDeviceUseCase RegisterDevice(
        FakeAuthorizationBoundary auth,
        FakeNodeStore nodes,
        FakeDeviceStore devices)
        => new(auth, nodes, devices, new FakeIdempotencyStore(), new FakeAuditEventWriter());

    [Fact]
    public async Task DiscoverDeviceIsReadOnlyAndCaptureIsIdempotentByHash()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeConnectionProfileReadStore profiles = new();
        FakeRouterOsReadPort routerOs = new();
        FakeSnapshotCapturePort capture = new();
        FakeSnapshotStore snapshots = new();
        FakeAuditEventWriter audit = new();

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
        DeviceView device = (await RegisterDevice(auth, nodes, devices).ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                NodeId = node.Id,
                DisplayName = "core",
                ManagementHost = "192.0.2.1",
                Role = DeviceRole.Router,
            })).Value!;

        profiles.ByDevice[device.Id] = new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(Guid.NewGuid()),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "ca",
        };

        ApplicationResult<DeviceDiscoveryView> discovery =
            await new DiscoverDeviceUseCase(auth, devices, profiles, routerOs).ExecuteAsync(
                new DiscoverDeviceCommand { Actor = "a", DeviceId = device.Id });
        Assert.True(discovery.IsSuccess);
        Assert.False(discovery.Value!.RouterOsMutated);
        Assert.False(routerOs.MutatedRouterOs);

        Guid idempotencyKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        CaptureSnapshotUseCase captureUseCase = new(auth, devices, profiles, capture, snapshots, audit);
        ApplicationResult<SnapshotView> first = await captureUseCase.ExecuteAsync(
            new CaptureSnapshotCommand { Actor = "a", DeviceId = device.Id, IdempotencyKey = idempotencyKey });
        ApplicationResult<SnapshotView> second = await captureUseCase.ExecuteAsync(
            new CaptureSnapshotCommand { Actor = "a", DeviceId = device.Id, IdempotencyKey = idempotencyKey });

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Equal(1, capture.CaptureCount);

        ApplicationResult<SnapshotListPageView> list =
            await new ListSnapshotsUseCase(auth, snapshots).ExecuteAsync(
                new ListSnapshotsQuery { Actor = "a", DeviceId = device.Id });
        Assert.True(list.IsSuccess);
        Assert.Single(list.Value!.Items);

        capture.NextResult = FakeSnapshotCapturePort.CreateResult(Enumerable.Repeat((byte)2, 32).ToArray());
        ApplicationResult<SnapshotView> third = await captureUseCase.ExecuteAsync(
            new CaptureSnapshotCommand
            {
                Actor = "a",
                DeviceId = device.Id,
                IdempotencyKey = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            });
        Assert.NotEqual(first.Value.Id, third.Value!.Id);

        ApplicationResult<SnapshotDiffView> diff = await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "a",
                LeftSnapshotId = first.Value.Id,
                RightSnapshotId = third.Value.Id,
            });
        Assert.True(diff.IsSuccess);
        Assert.False(diff.Value!.Identical);
        Assert.Contains("snapshot_hash", diff.Value.ChangedFields);

        ApplicationResult<SnapshotView> got = await new GetSnapshotUseCase(auth, snapshots).ExecuteAsync(
            new GetSnapshotQuery { Actor = "a", SnapshotId = first.Value.Id });
        Assert.True(got.IsSuccess);
        Assert.Equal(1, got.Value!.SchemaVersion);

        ApplicationResult<SnapshotDiffView> identical = await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "a",
                LeftSnapshotId = first.Value.Id,
                RightSnapshotId = first.Value.Id,
            });
        Assert.True(identical.Value!.Identical);
        Assert.Empty(identical.Value.ChangedFields);
    }

    [Fact]
    public async Task SnapshotUseCasesSurfaceNotFoundProfileMissingAndAuthFailures()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeConnectionProfileReadStore profiles = new();
        FakeRouterOsReadPort routerOs = new();
        FakeSnapshotCapturePort capture = new();
        FakeSnapshotStore snapshots = new();
        FakeAuditEventWriter audit = new();
        Guid missingId = Guid.NewGuid();
        Guid idempotencyKey = Guid.Parse("33333333-3333-3333-3333-333333333333");

        ApplicationResult<DeviceDiscoveryView> discoverMissing =
            await new DiscoverDeviceUseCase(auth, devices, profiles, routerOs).ExecuteAsync(
                new DiscoverDeviceCommand { Actor = "a", DeviceId = missingId });
        Assert.Equal("not_found", discoverMissing.Error!.Code);

        ApplicationResult<SnapshotView> captureMissing =
            await new CaptureSnapshotUseCase(auth, devices, profiles, capture, snapshots, audit).ExecuteAsync(
                new CaptureSnapshotCommand { Actor = "a", DeviceId = missingId, IdempotencyKey = idempotencyKey });
        Assert.Equal("not_found", captureMissing.Error!.Code);

        ApplicationResult<SnapshotView> getMissing =
            await new GetSnapshotUseCase(auth, snapshots).ExecuteAsync(
                new GetSnapshotQuery { Actor = "a", SnapshotId = missingId });
        Assert.Equal("not_found", getMissing.Error!.Code);

        ApplicationResult<SnapshotDiffView> compareMissing =
            await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
                new CompareSnapshotsQuery
                {
                    Actor = "a",
                    LeftSnapshotId = missingId,
                    RightSnapshotId = missingId,
                });
        Assert.Equal("not_found", compareMissing.Error!.Code);

        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        SiteView site = (await CreateSite(auth, sites).ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                Code = "LAB02",
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
        DeviceView device = (await RegisterDevice(auth, nodes, devices).ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                NodeId = node.Id,
                DisplayName = "core",
                ManagementHost = "192.0.2.2",
                Role = DeviceRole.Router,
            })).Value!;

        ApplicationResult<DeviceDiscoveryView> noProfile =
            await new DiscoverDeviceUseCase(auth, devices, profiles, routerOs).ExecuteAsync(
                new DiscoverDeviceCommand { Actor = "a", DeviceId = device.Id });
        Assert.Equal("failed", noProfile.Error!.Code);

        ApplicationResult<SnapshotView> captureNoProfile =
            await new CaptureSnapshotUseCase(auth, devices, profiles, capture, snapshots, audit).ExecuteAsync(
                new CaptureSnapshotCommand
                {
                    Actor = "a",
                    DeviceId = device.Id,
                    IdempotencyKey = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                });
        Assert.Equal("failed", captureNoProfile.Error!.Code);

        auth.DeniedPermissions.Add(ApplicationPermissions.DiscoveryRead);
        auth.DeniedPermissions.Add(ApplicationPermissions.SnapshotCapture);
        auth.DeniedPermissions.Add(ApplicationPermissions.SnapshotRead);
        auth.DeniedPermissions.Add(ApplicationPermissions.SnapshotCompare);

        Assert.Equal(
            "forbidden",
            (await new DiscoverDeviceUseCase(auth, devices, profiles, routerOs).ExecuteAsync(
                new DiscoverDeviceCommand { Actor = "guest", DeviceId = device.Id })).Error!.Code);
        Assert.Equal(
            "forbidden",
            (await new CaptureSnapshotUseCase(auth, devices, profiles, capture, snapshots, audit).ExecuteAsync(
                new CaptureSnapshotCommand
                {
                    Actor = "guest",
                    DeviceId = device.Id,
                    IdempotencyKey = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                })).Error!.Code);
        Assert.Equal(
            "forbidden",
            (await new GetSnapshotUseCase(auth, snapshots).ExecuteAsync(
                new GetSnapshotQuery { Actor = "guest", SnapshotId = missingId })).Error!.Code);
        Assert.Equal(
            "forbidden",
            (await new ListSnapshotsUseCase(auth, snapshots).ExecuteAsync(
                new ListSnapshotsQuery { Actor = "guest", DeviceId = device.Id })).Error!.Code);
        Assert.Equal(
            "forbidden",
            (await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
                new CompareSnapshotsQuery
                {
                    Actor = "guest",
                    LeftSnapshotId = missingId,
                    RightSnapshotId = missingId,
                })).Error!.Code);
    }

    [Fact]
    public async Task CoordinateStableReadMapsUnstableAndDoesNotMarkComplete()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeConnectionProfileReadStore profiles = new();
        FakeStableReadCoordinatorPort coordinator = new();

        SiteView site = (await CreateSite(auth, sites).ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                Code = "STB01",
                Name = "Stable",
            })).Value!;
        NodeView node = (await CreateNode(auth, sites, nodes).ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                SiteId = site.Id,
                Name = "edge",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            })).Value!;
        DeviceView device = (await RegisterDevice(auth, nodes, devices).ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                IdempotencyKey = Guid.NewGuid(),
                NodeId = node.Id,
                DisplayName = "edge",
                ManagementHost = "192.0.2.8",
                Role = DeviceRole.Router,
            })).Value!;
        profiles.ByDevice[device.Id] = new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(Guid.NewGuid()),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "ca",
        };

        CoordinateStableReadUseCase useCase = new(auth, devices, profiles, coordinator);
        ApplicationResult<StableReadCoordinationResult> ok = await useCase.ExecuteAsync(
            new CoordinateStableReadCommand { Actor = "a", DeviceId = device.Id });
        Assert.True(ok.IsSuccess);
        Assert.True(ok.Value!.IsComplete);

        coordinator.NextResult = new StableReadCoordinationResult
        {
            Outcome = StableReadOutcomeCodes.SnapshotUnstable,
            AttemptsUsed = 3,
            ConfigurationFingerprintHex = null,
            DiscoverySectionDigests = null,
        };
        ApplicationResult<StableReadCoordinationResult> unstable = await useCase.ExecuteAsync(
            new CoordinateStableReadCommand { Actor = "a", DeviceId = device.Id });
        Assert.True(unstable.IsFailure);
        Assert.Equal("snapshot_unstable", unstable.Error!.Code);
        Assert.False(coordinator.NextResult.IsComplete);
    }

    [Fact]
    public async Task CompareSnapshotsRequiresCompletedSnapshotsAndFallsBackToHashFields()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSnapshotStore snapshots = new();
        DeviceId deviceId = new(Guid.NewGuid());
        StoredSnapshot failed = new()
        {
            Metadata = SnapshotMetadata.CreateFailed(deviceId, DateTimeOffset.UtcNow),
            SchemaVersion = 1,
        };
        await snapshots.AddAsync(failed);

        ApplicationResult<SnapshotDiffView> notCompleted = await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "a",
                LeftSnapshotId = failed.Metadata.Id.Value,
                RightSnapshotId = failed.Metadata.Id.Value,
            });
        Assert.Equal("snapshot_not_completed", notCompleted.Error!.Code);

        byte[] digestA = Enumerable.Repeat((byte)9, 32).ToArray();
        byte[] digestB = Enumerable.Repeat((byte)8, 32).ToArray();
        Hash256 hashA = Hash256.Create(digestA);
        Hash256 hashB = Hash256.Create(digestB);
        StoredSnapshot left = new()
        {
            Metadata = SnapshotMetadata.CreateCompleted(
                deviceId,
                ConfigurationHash.FromDigest(hashA),
                ObservationHash.FromDigest(hashA),
                CapabilityHash.FromDigest(hashA),
                SnapshotHash.FromDigest(hashA),
                DateTimeOffset.UtcNow),
            SchemaVersion = 1,
        };
        StoredSnapshot right = new()
        {
            Metadata = SnapshotMetadata.CreateCompleted(
                deviceId,
                ConfigurationHash.FromDigest(hashB),
                ObservationHash.FromDigest(hashB),
                CapabilityHash.FromDigest(hashB),
                SnapshotHash.FromDigest(hashB),
                DateTimeOffset.UtcNow),
            SchemaVersion = 1,
        };
        await snapshots.AddAsync(left);
        await snapshots.AddAsync(right);

        ApplicationResult<SnapshotDiffView> mixed = await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "a",
                LeftSnapshotId = left.Metadata.Id.Value,
                RightSnapshotId = right.Metadata.Id.Value,
            });
        Assert.True(mixed.IsSuccess);
        Assert.False(mixed.Value!.Identical);
        Assert.Empty(mixed.Value.Entries);
        Assert.Contains("configuration_hash", mixed.Value.ChangedFields);
        Assert.Contains("observation_hash", mixed.Value.ChangedFields);
        Assert.Contains("capability_hash", mixed.Value.ChangedFields);
        Assert.Contains("snapshot_hash", mixed.Value.ChangedFields);
    }

    [Fact]
    public async Task AuthorizationBoundaryIsHonored()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.InventoryWrite);
        FakeSiteStore sites = new();

        ApplicationResult<SiteView> result = await CreateSite(auth, sites).ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = "guest",
                IdempotencyKey = Guid.NewGuid(),
                Code = "NOPE1",
                Name = "Nope",
            });
        Assert.False(result.IsSuccess);
        Assert.Equal("forbidden", result.Error!.Code);
    }
}

public sealed class ApplicationResultTests
{
    [Fact]
    public void ApplicationResultsFactoriesAndErrorCodes()
    {
        ApplicationResult<int> ok = ApplicationResults.Ok(42);
        Assert.True(ok.IsSuccess);
        Assert.False(ok.IsFailure);
        Assert.Equal(42, ok.Value);
        Assert.Null(ok.Error);

        ApplicationResult<int> fail = ApplicationResults.Fail(ApplicationError.Unauthorized("x"));
        Assert.True(fail.IsFailure);
        Assert.Equal("unauthorized", fail.Error!.Code);

        Assert.Equal("dependency", ApplicationError.Dependency("d").Code);
        Assert.Equal("forbidden", ApplicationError.Forbidden().Code);
        Assert.Equal("snapshot_unstable", ApplicationError.SnapshotUnstable().Code);
    }
}
