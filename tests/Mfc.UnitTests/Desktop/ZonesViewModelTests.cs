using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W3.5: Zones panel UpdateZoneDefinition + ResolveZonesForDevice from selection.</summary>
public sealed class ZonesViewModelTests
{
    [Fact]
    public async Task UpdateZoneCommandSendsNameDescriptionAndRowVersion()
    {
        RecordingZones panel = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using ZonesViewModel vm = new(panel, connection, inventory);
        ZoneDefinitionListItem zone = new()
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Key = "lan",
            Name = "LAN",
            OwnerScopeText = "Company",
            Description = "old",
            RowVersion = 3,
        };
        vm.Zones.Add(zone);
        vm.SelectedZone = zone;
        vm.EditZoneName = "LAN-core";
        vm.EditZoneDescription = "updated";

        await vm.UpdateZoneCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.UpdateCalls);
        Assert.Equal(zone.Id, panel.LastUpdateZoneId);
        Assert.Equal(3UL, panel.LastUpdateRowVersion);
        Assert.Equal("LAN-core", panel.LastUpdateName);
        Assert.Equal("updated", panel.LastUpdateDescription);
        Assert.False(panel.LastResetDescription);
        Assert.Equal("LAN-core", vm.SelectedZone?.Name);
        Assert.Equal(4UL, vm.SelectedZone?.RowVersion);
    }

    [Fact]
    public async Task UpdateZoneCommandClearsDescriptionWhenEditFieldEmpty()
    {
        RecordingZones panel = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using ZonesViewModel vm = new(panel, connection, inventory);
        ZoneDefinitionListItem zone = new()
        {
            Id = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            Key = "wan",
            Name = "WAN",
            OwnerScopeText = "Company",
            Description = "drop-me",
            RowVersion = 1,
        };
        vm.Zones.Add(zone);
        vm.SelectedZone = zone;
        vm.EditZoneDescription = "   ";

        await vm.UpdateZoneCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.True(panel.LastResetDescription);
        Assert.Null(panel.LastUpdateDescription);
        Assert.Null(vm.SelectedZone?.Description);
    }

    [Fact]
    public async Task ResolveDeviceCommandCallsPanelWithSelectedDeviceId()
    {
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        RecordingZones panel = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        InventoryNodeViewModel site = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Site,
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DisplayName = "LAB",
            Children =
            [
                new InventoryTreeItem
                {
                    Kind = InventoryTreeKind.Node,
                    Id = nodeId,
                    DisplayName = "core",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceId,
                            DisplayName = "chr-seed",
                        },
                    ],
                },
            ],
        });
        inventory.Roots.Add(site);
        inventory.SelectedNode = site.Children[0].Children[0];

        using ZonesViewModel vm = new(panel, connection, inventory);
        await vm.ResolveDeviceCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.ResolveDeviceCalls);
        Assert.Equal(deviceId, panel.LastResolveDeviceId);
        Assert.Equal(0, panel.ResolveNodeCalls);
        Assert.Contains("ZONE_OBSERVATION_UNAVAILABLE", Assert.Single(vm.ResolveResults).BlockerLines[0]);
    }

    [Fact]
    public async Task ResolveDeviceWithoutDeviceSelectionSetsError()
    {
        RecordingZones panel = new();
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using ZonesViewModel vm = new(panel, connection, inventory);

        await vm.ResolveDeviceCommand.ExecuteAsync(null);

        Assert.Equal(0, panel.ResolveDeviceCalls);
        Assert.Contains("Select a Device", vm.ErrorText, StringComparison.Ordinal);
    }

    private sealed class RecordingZones : IZonePanelService
    {
        public int UpdateCalls { get; private set; }

        public int ResolveDeviceCalls { get; private set; }

        public int ResolveNodeCalls { get; private set; }

        public Guid LastUpdateZoneId { get; private set; }

        public ulong LastUpdateRowVersion { get; private set; }

        public string? LastUpdateName { get; private set; }

        public string? LastUpdateDescription { get; private set; }

        public bool LastResetDescription { get; private set; }

        public Guid LastResolveDeviceId { get; private set; }

        public Task<IReadOnlyList<ZoneDefinitionListItem>> ListZonesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZoneDefinitionListItem>>([]);

        public Task<ZoneDefinitionListItem> CreateCompanyZoneAsync(
            string key,
            string name,
            string? description,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ZoneDefinitionListItem> UpdateZoneAsync(
            ZoneDefinitionListItem zone,
            string name,
            string? description,
            bool resetDescription,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            LastUpdateZoneId = zone.Id;
            LastUpdateRowVersion = zone.RowVersion;
            LastUpdateName = name;
            LastUpdateDescription = description;
            LastResetDescription = resetDescription;
            return Task.FromResult(new ZoneDefinitionListItem
            {
                Id = zone.Id,
                Key = zone.Key,
                Name = name,
                OwnerScopeText = zone.OwnerScopeText,
                OwnerId = zone.OwnerId,
                Description = resetDescription ? null : description,
                RowVersion = zone.RowVersion + 1,
            });
        }

        public Task DeleteZoneAsync(
            ZoneDefinitionListItem zone,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<NodeZoneBindingListItem>> ListBindingsAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NodeZoneBindingListItem>>([]);

        public Task<NodeZoneBindingListItem> UpsertBindingAsync(
            Guid nodeId,
            Guid zoneId,
            NodeZoneBindingKind kind,
            IReadOnlyList<string> values,
            ulong? expectedRowVersion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteBindingAsync(
            NodeZoneBindingListItem binding,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForNodeAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
        {
            ResolveNodeCalls++;
            return Task.FromResult<IReadOnlyList<ZoneResolveResultListItem>>([]);
        }

        public Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForDeviceAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            ResolveDeviceCalls++;
            LastResolveDeviceId = deviceId;
            return Task.FromResult<IReadOnlyList<ZoneResolveResultListItem>>(
            [
                new ZoneResolveResultListItem
                {
                    DeviceId = deviceId,
                    ZoneId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000"),
                    MembersText = string.Empty,
                    AnalysisStale = true,
                    BlockerLines = ["ZONE_OBSERVATION_UNAVAILABLE: no capture"],
                },
            ]);
        }
    }

    private sealed class FakeConnection : IControllerConnectionService
    {
        public ControllerConnectionState State { get; set; } = ControllerConnectionState.Disconnected;

        public string? LastError => null;

        public Grpc.Net.Client.GrpcChannel? Channel => null;

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyTreeService : IInventoryTreeService
    {
        public InventoryTreeLoadResult Current { get; } = new()
        {
            Roots = [],
            Succeeded = true,
            IsCached = false,
            IsRefreshing = false,
        };

        public Task<InventoryTreeLoadResult> RefreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Current);
    }
}
