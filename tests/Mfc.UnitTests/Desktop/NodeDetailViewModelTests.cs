using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W1.6: Node module lists device members with explicit fields from inventory tree.</summary>
public sealed class NodeDetailViewModelTests
{
    [Fact]
    public void SelectingNodeFillsDeviceMembersWithoutInventingVrrp()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeConnection connection = new();
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
                    NodeKindText = "Router",
                    UplinkModeText = "One",
                    StatusText = "Active",
                    WorkflowStatusText = "Synchronized",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceId,
                            DisplayName = "chr-seed",
                            ReachabilityText = "Reachable",
                            ModelText = "CHR",
                            RouterOsVersionText = "7.16.2",
                            VrrpRolesText = "—",
                            LastSnapshotText = "2026-08-30 10:00:00Z",
                            SupportStateText = "Supported",
                            DesiredHashText = "aa",
                            CommittedHashText = "bb",
                            ActualHashText = "cc",
                        },
                    ],
                },
            ],
        });
        inventory.Roots.Add(site);

        using NodeDetailViewModel vm = new(inventory, new ZonesViewModel(new StubZones(), connection, inventory), new OnboardingViewModel(new StubOnboarding(), connection, inventory));
        inventory.SelectedNode = site.Children[0];

        InventoryNodeViewModel member = Assert.Single(vm.DeviceMembers);
        Assert.Equal("chr-seed", member.DisplayName);
        Assert.Equal("Reachable", member.ReachabilityText);
        Assert.Equal("CHR", member.ModelText);
        Assert.Equal("7.16.2", member.RouterOsVersionText);
        Assert.Equal("2026-08-30 10:00:00Z", member.LastSnapshotText);
        Assert.False(member.HasVrrpRoles);
        Assert.True(vm.HasDeviceMembers);
        Assert.False(vm.HasNoDeviceMembers);
        Assert.Contains("chr-seed", Assert.Single(vm.DeviceHashLines), StringComparison.Ordinal);
    }

    [Fact]
    public void SelectingNodeShowsVrrpRolesWhenBackendLabelsExist()
    {
        Guid nodeId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        InventoryNodeViewModel site = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Site,
            Id = Guid.Parse("dddddddd-eeee-ffff-aaaa-111111111111"),
            DisplayName = "LAB",
            Children =
            [
                new InventoryTreeItem
                {
                    Kind = InventoryTreeKind.Node,
                    Id = nodeId,
                    DisplayName = "pair",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = Guid.Parse("22222222-3333-4444-5555-666666666666"),
                            DisplayName = "r1",
                            VrrpRolesText = "master",
                            ReachabilityText = "Reachable",
                        },
                    ],
                },
            ],
        });
        inventory.Roots.Add(site);

        using NodeDetailViewModel vm = new(inventory, new ZonesViewModel(new StubZones(), connection, inventory), new OnboardingViewModel(new StubOnboarding(), connection, inventory));
        inventory.SelectedNode = site.Children[0];

        InventoryNodeViewModel member = Assert.Single(vm.DeviceMembers);
        Assert.True(member.HasVrrpRoles);
        Assert.Equal("master", member.VrrpRolesText);
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

    private sealed class StubZones : IZonePanelService
    {
        public Task<IReadOnlyList<ZoneDefinitionListItem>> ListZonesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZoneDefinitionListItem>>([]);

        public Task<ZoneDefinitionListItem> CreateCompanyZoneAsync(
            string key,
            string name,
            string? description,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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
            => Task.FromResult<IReadOnlyList<ZoneResolveResultListItem>>([]);
    }

    private sealed class StubOnboarding : IOnboardingServiceClient
    {
        public Task<OnboardingPrerequisiteReport> ValidatePrerequisitesAsync(
            Guid nodeId,
            IReadOnlyList<OnboardingDevicePrerequisiteFacts> devices,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingPlanSummary> CreatePlanAsync(
            Guid nodeId,
            Sha256 membershipHash,
            Sha256 topologyHash,
            IReadOnlyList<OnboardingDevicePlanInput> devices,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingOperationSummary> StartAsync(
            Guid planId,
            Sha256 planHash,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<OnboardingProgress> WatchAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingOperationSummary> RollbackAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingRecoveryStatus> GetRecoveryStatusAsync(
            Guid nodeId,
            Guid? operationId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
