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

        CreateZoneDefinitionUseCase create = new(auth, zones, idempotency, audit, new FakeUnitOfWork());
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

        UpdateZoneDefinitionUseCase update = new(auth, zones, idempotency, audit, new FakeUnitOfWork());
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

        UpsertNodeZoneBindingUseCase upsert = new(auth, nodes, zones, bindings, idempotency, audit, new FakeUnitOfWork());
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

        UpsertNodeZoneBindingUseCase upsert = new(auth, nodes, zones, bindings, idempotency, audit, new FakeUnitOfWork());
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

    [Fact]
    public async Task ListDeleteAndUpsertUpdateCoverRemainingZoneUseCases()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeZoneDefinitionStore zones = new();
        FakeNodeZoneBindingStore bindings = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();

        Site site = Site.Create(SiteCode.Create("ZLAB"), NonEmptyName.Create("Zone Lab"));
        await sites.AddAsync(site);
        Node node = Node.Create(site.Id, NonEmptyName.Create("edge"), NodeKind.Router, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);

        CreateZoneDefinitionUseCase create = new(auth, zones, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<ZoneDefinitionView> created = await create.ExecuteAsync(new CreateZoneDefinitionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            OwnerScope = PolicyOwnerScope.Company,
            Key = "dmz",
            Name = "DMZ",
            Description = "perimeter",
        });
        Assert.True(created.IsSuccess);

        ApplicationResult<ZoneDefinitionView> duplicateKey = await create.ExecuteAsync(new CreateZoneDefinitionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            OwnerScope = PolicyOwnerScope.Company,
            Key = "dmz",
            Name = "DMZ2",
        });
        Assert.True(duplicateKey.IsFailure);
        Assert.Equal("conflict", duplicateKey.Error!.Code);

        ListZoneDefinitionsUseCase listZones = new(auth, zones);
        ApplicationResult<IReadOnlyList<ZoneDefinitionView>> listed = await listZones.ExecuteAsync(
            new ListZoneDefinitionsQuery
            {
                Actor = "admin",
                OwnerScope = PolicyOwnerScope.Company,
            });
        Assert.True(listed.IsSuccess);
        Assert.Contains(listed.Value!, z => z.Key == "dmz");

        UpdateZoneDefinitionUseCase update = new(auth, zones, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<ZoneDefinitionView> cleared = await update.ExecuteAsync(new UpdateZoneDefinitionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            ZoneId = created.Value!.Id,
            ExpectedRowVersion = created.Value.RowVersion,
            ClearDescription = true,
        });
        Assert.True(cleared.IsSuccess);
        Assert.Null(cleared.Value!.Description);

        ApplicationResult<ZoneDefinitionView> described = await update.ExecuteAsync(new UpdateZoneDefinitionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            ZoneId = created.Value.Id,
            ExpectedRowVersion = cleared.Value.RowVersion,
            Description = "updated",
        });
        Assert.True(described.IsSuccess);
        Assert.Equal("updated", described.Value!.Description);

        UpsertNodeZoneBindingUseCase upsert = new(auth, nodes, zones, bindings, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<NodeZoneBindingView> binding = await upsert.ExecuteAsync(new UpsertNodeZoneBindingCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            NodeId = node.Id.Value,
            ZoneId = created.Value.Id,
            Kind = NodeZoneBindingKind.InterfaceList,
            Values = ["LAN"],
        });
        Assert.True(binding.IsSuccess);

        ApplicationResult<NodeZoneBindingView> missingRowVersion = await upsert.ExecuteAsync(new UpsertNodeZoneBindingCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            NodeId = node.Id.Value,
            ZoneId = created.Value.Id,
            Kind = NodeZoneBindingKind.InterfaceList,
            Values = ["WAN"],
        });
        Assert.True(missingRowVersion.IsFailure);
        Assert.Equal("validation", missingRowVersion.Error!.Code);

        Hash256 expectedDigest = NodeZoneBinding.ComputeDependencyHash(
            NodeZoneBindingKind.InterfaceList, ["WAN"], []);
        byte[] expectedHash = expectedDigest.Bytes.ToArray();
        ApplicationResult<NodeZoneBindingView> updatedBinding = await upsert.ExecuteAsync(new UpsertNodeZoneBindingCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            NodeId = node.Id.Value,
            ZoneId = created.Value.Id,
            Kind = NodeZoneBindingKind.InterfaceList,
            Values = ["WAN"],
            ExpectedDependencyHash = expectedHash,
            ExpectedRowVersion = binding.Value!.RowVersion,
        });
        Assert.True(updatedBinding.IsSuccess);
        Assert.Equal(["WAN"], updatedBinding.Value!.Values);

        ApplicationResult<NodeZoneBindingView> badHash = await upsert.ExecuteAsync(new UpsertNodeZoneBindingCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            NodeId = node.Id.Value,
            ZoneId = created.Value.Id,
            Kind = NodeZoneBindingKind.InterfaceList,
            Values = ["WAN"],
            ExpectedDependencyHash = [1, 2, 3],
            ExpectedRowVersion = updatedBinding.Value.RowVersion,
        });
        Assert.True(badHash.IsFailure);
        Assert.Equal("validation", badHash.Error!.Code);

        ListNodeZoneBindingsUseCase listBindings = new(auth, nodes, bindings);
        ApplicationResult<IReadOnlyList<NodeZoneBindingView>> listedBindings = await listBindings.ExecuteAsync(
            new ListNodeZoneBindingsQuery { Actor = "admin", NodeId = node.Id.Value });
        Assert.True(listedBindings.IsSuccess);
        Assert.Single(listedBindings.Value!);

        DeleteZoneDefinitionUseCase deleteZone = new(auth, zones, bindings, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<bool> blockedDelete = await deleteZone.ExecuteAsync(new DeleteZoneDefinitionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            ZoneId = created.Value.Id,
            ExpectedRowVersion = described.Value.RowVersion,
        });
        Assert.True(blockedDelete.IsFailure);
        Assert.Equal("conflict", blockedDelete.Error!.Code);

        DeleteNodeZoneBindingUseCase deleteBinding = new(auth, bindings, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<bool> deletedBinding = await deleteBinding.ExecuteAsync(new DeleteNodeZoneBindingCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            BindingId = updatedBinding.Value.Id,
            ExpectedRowVersion = updatedBinding.Value.RowVersion,
        });
        Assert.True(deletedBinding.IsSuccess);

        ApplicationResult<bool> deletedZone = await deleteZone.ExecuteAsync(new DeleteZoneDefinitionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            ZoneId = created.Value.Id,
            ExpectedRowVersion = described.Value.RowVersion,
        });
        Assert.True(deletedZone.IsSuccess);

        ApplicationResult<bool> missingBinding = await deleteBinding.ExecuteAsync(new DeleteNodeZoneBindingCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            BindingId = Guid.NewGuid(),
            ExpectedRowVersion = 1,
        });
        Assert.True(missingBinding.IsFailure);
        Assert.Equal("not_found", missingBinding.Error!.Code);

        ApplicationResult<IReadOnlyList<NodeZoneBindingView>> missingNode = await listBindings.ExecuteAsync(
            new ListNodeZoneBindingsQuery { Actor = "admin", NodeId = Guid.NewGuid() });
        Assert.True(missingNode.IsFailure);
        Assert.Equal("not_found", missingNode.Error!.Code);
    }

    [Fact]
    public async Task ZoneReadPermissionRequiredForListOperations()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.ZoneRead);
        FakeZoneDefinitionStore zones = new();
        FakeNodeStore nodes = new();
        FakeNodeZoneBindingStore bindings = new();

        ListZoneDefinitionsUseCase listZones = new(auth, zones);
        ApplicationResult<IReadOnlyList<ZoneDefinitionView>> zonesResult = await listZones.ExecuteAsync(
            new ListZoneDefinitionsQuery { Actor = "viewer" });
        Assert.True(zonesResult.IsFailure);
        Assert.Equal("forbidden", zonesResult.Error!.Code);

        ListNodeZoneBindingsUseCase listBindings = new(auth, nodes, bindings);
        ApplicationResult<IReadOnlyList<NodeZoneBindingView>> bindingsResult = await listBindings.ExecuteAsync(
            new ListNodeZoneBindingsQuery { Actor = "viewer", NodeId = Guid.NewGuid() });
        Assert.True(bindingsResult.IsFailure);
        Assert.Equal("forbidden", bindingsResult.Error!.Code);
    }
}
