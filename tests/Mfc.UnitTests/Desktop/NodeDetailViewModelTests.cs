using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W1.6 device members + W3.4 GetNodeWorkflow + W4.1 VRRP members table on the Node module.</summary>
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
        inventory.SelectedNode = site.Children[0];

        RecordingInventoryClient client = new();
        using NodeDetailViewModel vm = CreateVm(inventory, connection, client);

        InventoryNodeViewModel member = Assert.Single(vm.DeviceMembers);
        Assert.Equal("chr-seed", member.DisplayName);
        Assert.Equal("Reachable", member.ReachabilityText);
        Assert.Equal("CHR", member.ModelText);
        Assert.Equal("7.16.2", member.RouterOsVersionText);
        Assert.Equal("2026-08-30 10:00:00Z", member.LastSnapshotText);
        Assert.False(member.HasVrrpRoles);
        Assert.True(vm.HasDeviceMembers);
        Assert.False(vm.IsVrrpNode);
        Assert.Empty(vm.VrrpMembers);
        Assert.True(vm.HasStandaloneDeviceList);
        Assert.False(vm.HasNoDeviceMembers);
        Assert.Contains("chr-seed", Assert.Single(vm.DeviceHashLines), StringComparison.Ordinal);
        Assert.Equal(0, client.GetNodeWorkflowCalls);
        Assert.Contains("Connect to Controller", vm.DeploymentReadinessText, StringComparison.Ordinal);
        Assert.DoesNotContain("Zones hint=", vm.DeploymentReadinessText, StringComparison.Ordinal);
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
        inventory.SelectedNode = site.Children[0];

        using NodeDetailViewModel vm = CreateVm(inventory, connection);

        InventoryNodeViewModel member = Assert.Single(vm.DeviceMembers);
        Assert.True(member.HasVrrpRoles);
        Assert.Equal("master", member.VrrpRolesText);
    }

    [Fact]
    public void VrrpNodeBuildsAbMemberTableWithoutInventingRoles()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceA = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceB = Guid.Parse("22222222-3333-4444-5555-666666666666");
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
                    DisplayName = "edge-pair",
                    NodeKindText = "Vrrp",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceA,
                            DisplayName = "r1",
                            VrrpRolesText = "master",
                            ManagementHostText = "192.0.2.1:8729",
                            LastSnapshotText = "2026-08-30 10:00:00Z",
                            ReachabilityText = "Reachable",
                        },
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceB,
                            DisplayName = "r2",
                            VrrpRolesText = "—",
                            ManagementHostText = "192.0.2.2:8729",
                            LastSnapshotText = "—",
                            ReachabilityText = "Unknown",
                        },
                    ],
                },
            ],
        });
        inventory.Roots.Add(site);
        inventory.SelectedNode = site.Children[0];

        using NodeDetailViewModel vm = CreateVm(inventory, connection);

        Assert.True(vm.IsVrrpNode);
        Assert.False(vm.ShowStandaloneDeviceSection);
        Assert.Contains("pair consistency", vm.VrrpPairHint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, vm.VrrpMembers.Count);
        Assert.Equal("a", vm.VrrpMembers[0].SlotText);
        Assert.Equal("r1", vm.VrrpMembers[0].DisplayName);
        Assert.Equal("master", vm.VrrpMembers[0].RoleText);
        Assert.True(vm.VrrpMembers[0].HasRole);
        Assert.Equal("192.0.2.1:8729", vm.VrrpMembers[0].ManagementHostText);
        Assert.Equal("2026-08-30 10:00:00Z", vm.VrrpMembers[0].LastSnapshotText);
        Assert.Equal("b", vm.VrrpMembers[1].SlotText);
        Assert.Equal("r2", vm.VrrpMembers[1].DisplayName);
        Assert.Equal("—", vm.VrrpMembers[1].RoleText);
        Assert.False(vm.VrrpMembers[1].HasRole);
        Assert.DoesNotContain("Backup", vm.VrrpMembers[1].RoleText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Master", vm.VrrpMembers[1].SummaryLine, StringComparison.OrdinalIgnoreCase);

        vm.SelectedVrrpMember = vm.VrrpMembers[1];
        Assert.True(vm.HasSelectedVrrpMember);
        Assert.Equal(deviceB, vm.SelectedVrrpMember!.DeviceId);
    }

    [Fact]
    public async Task RefreshLoadsGetNodeWorkflowDeviceContributingStatus()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
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
                    WorkflowStatusText = "Synchronized",
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
        inventory.SelectedNode = site.Children[0];

        RecordingInventoryClient client = new()
        {
            Workflow = new NodeWorkflow
            {
                NodeId = DesktopProtoUuid.FromGuid(nodeId),
                WorkflowStatus = NodeWorkflowStatus.Drifted,
                Devices =
                {
                    new DeviceWorkflowProjection
                    {
                        DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                        SyncClassification = DeviceSyncClassification.Drifted,
                        ContributingStatus = NodeWorkflowStatus.CaptureRequired,
                    },
                },
            },
        };

        using NodeDetailViewModel vm = CreateVm(inventory, connection, client);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.True(client.GetNodeWorkflowCalls >= 1);
        Assert.Equal(nodeId, client.LastWorkflowNodeId);
        Assert.Equal("Drifted", vm.WorkflowStatusText);
        Assert.Equal("Drifted", vm.DeploymentReadinessText);
        Assert.DoesNotContain("Zones hint=", vm.DeploymentReadinessText, StringComparison.Ordinal);
        string line = Assert.Single(vm.WorkflowDeviceLines);
        Assert.Contains("chr-seed", line, StringComparison.Ordinal);
        Assert.Contains("CaptureRequired", line, StringComparison.Ordinal);
        Assert.Contains("Drifted", line, StringComparison.Ordinal);
        Assert.True(vm.HasWorkflowDeviceLines);
        Assert.False(vm.HasNoWorkflowDeviceLines);
    }

    private static NodeDetailViewModel CreateVm(
        InventoryTreeViewModel inventory,
        FakeConnection connection,
        IInventoryTreeClient? client = null)
    {
        return new NodeDetailViewModel(
            inventory,
            new ZonesViewModel(new StubZones(), connection, inventory),
            new OnboardingViewModel(new StubOnboarding(), connection, inventory),
            client ?? new RecordingInventoryClient(),
            new StubSnapshotClient(),
            connection);
    }

    private sealed class RecordingInventoryClient : IInventoryTreeClient
    {
        public NodeWorkflow Workflow { get; init; } = new();

        public int GetNodeWorkflowCalls { get; private set; }

        public Guid LastWorkflowNodeId { get; private set; }

        public Task<IReadOnlyList<Site>> ListAllSitesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Site>>([]);

        public Task<IReadOnlyList<Node>> ListAllNodesAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Node>>([]);

        public Task<NodeDetails> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
            => Task.FromResult(new NodeDetails());

        public Task<NodeWorkflow> GetNodeWorkflowAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
        {
            GetNodeWorkflowCalls++;
            LastWorkflowNodeId = nodeId;
            return Task.FromResult(Workflow);
        }

        public Task<Site> CreateSiteAsync(string code, string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Node> CreateNodeAsync(
            Guid siteId,
            string name,
            NodeKind declaredKind,
            DeclaredUplinkMode declaredUplinkMode,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Device> RegisterDeviceAsync(
            Guid nodeId,
            string displayName,
            string managementHost,
            uint managementPort,
            DeviceRole role,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeviceConnectionSummary> UpdateDeviceConnectionAsync(
            Guid deviceId,
            string username,
            ReadOnlyMemory<byte> passwordUtf8,
            CertificateTrustMode trustMode,
            string? caProfileRef,
            Sha256? pinnedSpkiSha256,
            uint connectTimeoutMs,
            uint commandTimeoutMs,
            ulong maxResponseBytes,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ValidateDeviceConnectionResponse> ValidateDeviceConnectionAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ListNeighborCandidatesResponse> ListNeighborCandidatesAsync(
            Guid seedDeviceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VrrpPairConsistencyReport> ValidateVrrpPairConsistencyAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new VrrpPairConsistencyReport { Passed = true });
    }

    private sealed class StubSnapshotClient : ISnapshotViewerClient
    {
        public Task<StartCaptureResponse> StartCaptureAsync(
            Guid deviceId, Guid idempotencyKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<CaptureProgress> WatchCaptureAsync(
            Guid operationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<IReadOnlyList<SnapshotSummary>> ListCapturesAsync(
            Guid deviceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SnapshotSummary>>([]);

        public Task<SnapshotSummary> GetSummaryAsync(
            Guid captureId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SnapshotRecord>> GetAllSectionRecordsAsync(
            Guid captureId, string sectionId, DiffDomain domain, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SnapshotRecord>>([]);

        public Task<DiffPage> CompareSnapshotsAsync(
            Guid leftCaptureId, Guid rightCaptureId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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

        public Task<ZoneDefinitionListItem> UpdateZoneAsync(
            ZoneDefinitionListItem zone,
            string name,
            string? description,
            bool resetDescription,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForDeviceAsync(
            Guid deviceId,
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
