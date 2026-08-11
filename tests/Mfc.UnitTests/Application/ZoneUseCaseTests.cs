using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Zones;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class ZoneUseCaseTests
{
    [Fact]
    public async Task CreateUpdateAndConflictOnStaleRowVersion()
    {
        FakeAuthorizationBoundary auth = new();
        FakeZoneDefinitionStore zones = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();

        CreateZoneDefinitionUseCase create = new(auth, zones, idempotency, audit);
        ApplicationResult<ZoneDefinitionView> created = await create.ExecuteAsync(new CreateZoneDefinitionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            OwnerScope = PolicyOwnerScope.Company,
            Key = "lan",
            Name = "LAN",
        });
        Assert.True(created.IsSuccess);
        Assert.Equal(1UL, created.Value!.RowVersion);

        UpdateZoneDefinitionUseCase update = new(auth, zones, idempotency, audit);
        ApplicationResult<ZoneDefinitionView> updated = await update.ExecuteAsync(new UpdateZoneDefinitionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            ZoneId = created.Value.Id,
            ExpectedRowVersion = 1,
            Name = "LAN Core",
        });
        Assert.True(updated.IsSuccess);
        Assert.Equal(2UL, updated.Value!.RowVersion);

        ApplicationResult<ZoneDefinitionView> conflict = await update.ExecuteAsync(new UpdateZoneDefinitionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            ZoneId = created.Value.Id,
            ExpectedRowVersion = 1,
            Name = "stale",
        });
        Assert.True(conflict.IsFailure);
        Assert.Equal("conflict", conflict.Error!.Code);
    }

    [Fact]
    public async Task UpsertBindingAndResolvePersistsAnalysisStale()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeZoneDefinitionStore zones = new();
        FakeNodeZoneBindingStore bindings = new();
        FakeZoneResolveObservationSource observations = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();

        Site site = Site.Create(SiteCode.Create("LAB"), NonEmptyName.Create("Lab"));
        await sites.AddAsync(site);
        Node node = Node.Create(site.Id, NonEmptyName.Create("core"), NodeKind.Router, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);
        Device device = node.AddDevice(
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.10"),
            DeviceRole.Router);
        await devices.AddAsync(device);
        await nodes.UpdateAsync(node);

        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Company,
            null,
            NonEmptyName.Create("lan"),
            NonEmptyName.Create("LAN"));
        await zones.AddAsync(zone);

        UpsertNodeZoneBindingUseCase upsert = new(auth, nodes, zones, bindings, idempotency, audit);
        ApplicationResult<NodeZoneBindingView> binding = await upsert.ExecuteAsync(new UpsertNodeZoneBindingCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            NodeId = node.Id.Value,
            ZoneId = zone.Id.Value,
            Kind = NodeZoneBindingKind.SingleInterface,
            Values = ["ether1"],
        });
        Assert.True(binding.IsSuccess);
        Assert.True(binding.Value!.AnalysisStale);

        observations.ByDevice[device.Id.Value] = new ZoneResolveDeviceObservation
        {
            DeviceId = device.Id,
            Interfaces =
            [
                new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false },
            ],
            InterfaceLists = [],
            InterfaceListMembers = [],
            ObservationAvailable = true,
        };

        ResolveZonesForDeviceUseCase resolve = new(auth, devices, bindings, observations);
        ApplicationResult<ZoneResolveBatchView> resolved = await resolve.ExecuteAsync(new ResolveZonesForDeviceCommand
        {
            Actor = "admin",
            DeviceId = device.Id.Value,
        });
        Assert.True(resolved.IsSuccess);
        Assert.Single(resolved.Value!.Results);
        Assert.Contains("ether1", resolved.Value.Results[0].ResolvedMembers);
        // Expected hash at upsert used empty resolved members; live resolve includes ether1 → AC#9 stale.
        Assert.True(resolved.Value.Results[0].AnalysisStale);
        Assert.Empty(resolved.Value.Results[0].Blockers);
        Assert.True(resolved.Value.Results[0].Binding.AnalysisStale);
    }

    [Fact]
    public async Task ObservationUnavailableProducesTypedBlocker()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeZoneDefinitionStore zones = new();
        FakeNodeZoneBindingStore bindings = new();
        FakeZoneResolveObservationSource observations = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();

        Site site = Site.Create(SiteCode.Create("LAB2"), NonEmptyName.Create("Lab2"));
        await sites.AddAsync(site);
        Node node = Node.Create(site.Id, NonEmptyName.Create("core"), NodeKind.Router, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);
        Device device = node.AddDevice(
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.11"),
            DeviceRole.Router);
        await devices.AddAsync(device);

        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Company,
            null,
            NonEmptyName.Create("wan"),
            NonEmptyName.Create("WAN"));
        await zones.AddAsync(zone);

        UpsertNodeZoneBindingUseCase upsert = new(auth, nodes, zones, bindings, idempotency, audit);
        ApplicationResult<NodeZoneBindingView> binding = await upsert.ExecuteAsync(new UpsertNodeZoneBindingCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            NodeId = node.Id.Value,
            ZoneId = zone.Id.Value,
            Kind = NodeZoneBindingKind.SingleInterface,
            Values = ["ether1"],
        });
        Assert.True(binding.IsSuccess);

        ResolveZonesForDeviceUseCase resolve = new(auth, devices, bindings, observations);
        ApplicationResult<ZoneResolveBatchView> resolved = await resolve.ExecuteAsync(new ResolveZonesForDeviceCommand
        {
            Actor = "admin",
            DeviceId = device.Id.Value,
        });
        Assert.True(resolved.IsSuccess);
        Assert.Contains(
            resolved.Value!.Results[0].Blockers,
            b => b.Code == ZoneResolveBlockerCodes.ObservationUnavailable);
    }

    [Fact]
    public async Task ResolveRequiresZoneWritePermission()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.ZoneWrite);
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeNodeZoneBindingStore bindings = new();
        FakeZoneResolveObservationSource observations = new();

        Site site = Site.Create(SiteCode.Create("LAB"), NonEmptyName.Create("Lab"));
        await sites.AddAsync(site);
        Node node = Node.Create(site.Id, NonEmptyName.Create("core"), NodeKind.Router, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);
        Device device = node.AddDevice(
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.10"),
            DeviceRole.Router);
        await devices.AddAsync(device);
        await nodes.UpdateAsync(node);

        ResolveZonesForDeviceUseCase resolve = new(auth, devices, bindings, observations);
        ApplicationResult<ZoneResolveBatchView> result = await resolve.ExecuteAsync(new ResolveZonesForDeviceCommand
        {
            Actor = "viewer",
            DeviceId = device.Id.Value,
        });
        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task NodeResolveOrAccumulatesAnalysisStaleAcrossDevices()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeZoneDefinitionStore zones = new();
        FakeNodeZoneBindingStore bindings = new();
        FakeZoneResolveObservationSource observations = new();

        Site site = Site.Create(SiteCode.Create("LAB"), NonEmptyName.Create("Lab"));
        await sites.AddAsync(site);
        Node node = Node.Create(site.Id, NonEmptyName.Create("vrrp"), NodeKind.Vrrp, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);
        Device a = node.AddDevice(
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("10.0.0.1"),
            DeviceRole.Router);
        Device b = node.AddDevice(
            NonEmptyName.Create("r2"),
            ManagementEndpoint.Create("10.0.0.2"),
            DeviceRole.Router);
        await devices.AddAsync(a);
        await devices.AddAsync(b);
        await nodes.UpdateAsync(node);

        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Company, null, NonEmptyName.Create("lan"), NonEmptyName.Create("LAN"));
        await zones.AddAsync(zone);

        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.SingleInterface, ["ether1"], ["ether1"]);
        NodeZoneBinding created = NodeZoneBinding.Create(
            node.Id, zone.Id, NodeZoneBindingKind.SingleInterface, ["ether1"], expected);
        await bindings.AddAsync(created);

        observations.ByDevice[a.Id.Value] = new ZoneResolveDeviceObservation
        {
            DeviceId = a.Id,
            ObservationAvailable = true,
            Interfaces = [new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false }],
            InterfaceLists = [],
            InterfaceListMembers = [],
        };
        observations.ByDevice[b.Id.Value] = new ZoneResolveDeviceObservation
        {
            DeviceId = b.Id,
            ObservationAvailable = true,
            Interfaces = [new ZoneResolveInterfaceObservation { Name = "ether2", Dynamic = false }],
            InterfaceLists = [],
            InterfaceListMembers = [],
        };

        ResolveZonesForNodeUseCase resolve = new(auth, nodes, devices, bindings, observations);
        ApplicationResult<ZoneResolveBatchView> resolved = await resolve.ExecuteAsync(new ResolveZonesForNodeCommand
        {
            Actor = "admin",
            NodeId = node.Id.Value,
        });
        Assert.True(resolved.IsSuccess);

        NodeZoneBinding? persisted = await bindings.GetByNodeAndZoneAsync(node.Id, zone.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted!.AnalysisStale);
    }
}
