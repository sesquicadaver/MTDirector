using Google.Protobuf;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-NODE-01 / W7-90: Desktop Node VRRP pair Living Spec depth vs InventoryGrpcHost.</summary>
public sealed class DesktopNodeLivingSpecTests
{
    [Fact]
    public void Ac1WireAndDesktopClientExposeValidateVrrpPairConsistency()
    {
        Assert.Contains(
            InventoryService.Descriptor.Methods,
            static m => m.Name == "ValidateVrrpPairConsistency");
        Assert.NotNull(typeof(IInventoryTreeClient).GetMethod(nameof(IInventoryTreeClient.ValidateVrrpPairConsistencyAsync)));
        string source = ReadSource("src/Mfc.Desktop/Services/GrpcInventoryTreeClient.cs");
        Assert.Contains("ValidateVrrpPairConsistencyAsync", source, StringComparison.Ordinal);
        Assert.Contains("ValidateVrrpPairConsistencyRequest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2NodeViewModelExposesVrrpValidateCaptureAndFindings()
    {
        using NodeDetailViewModel vm = CreateVm(
            ControllerConnectionState.Connected,
            selectVrrpNode: true);

        Assert.NotNull(vm.GetType().GetProperty(nameof(NodeDetailViewModel.ValidateVrrpPairCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(NodeDetailViewModel.CaptureAllMembersAndValidateCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(NodeDetailViewModel.VrrpPairFindings)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(NodeDetailViewModel.VrrpPairStatusText)));
        Assert.True(vm.IsVrrpNode);
        Assert.NotNull(vm.ValidateVrrpPairCommand);
        Assert.NotNull(vm.CaptureAllMembersAndValidateCommand);
    }

    [Fact]
    public async Task Ac3ValidateVrrpPairLoadsFindingsWhenVrrpNodeConnected()
    {
        RecordingInventoryClient client = new()
        {
            Report = new VrrpPairConsistencyReport
            {
                Passed = false,
                MemberCount = 2,
                CaptureCount = 2,
            },
        };
        client.Report.Findings.Add(new VrrpPairConsistencyFinding
        {
            Code = "VIP_MISMATCH",
            Severity = "BLOCKER",
            Message = "VIP differs",
        });

        using NodeDetailViewModel vm = CreateVm(
            ControllerConnectionState.Connected,
            selectVrrpNode: true,
            client: client);
        await WaitUntilIdleAsync(vm);

        await vm.ValidateVrrpPairCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.ValidateVrrpCalls);
        Assert.Equal("VIP_MISMATCH", Assert.Single(vm.VrrpPairFindings).Code);
        Assert.Contains("blockers", vm.VrrpPairStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac4ValidateVrrpRequiresVrrpNodeAndConnectedController()
    {
        using NodeDetailViewModel disconnected = CreateVm(
            ControllerConnectionState.Disconnected,
            selectVrrpNode: true);
        Assert.False(disconnected.ValidateVrrpPairCommand.CanExecute(null));
        Assert.False(disconnected.CaptureAllMembersAndValidateCommand.CanExecute(null));

        using NodeDetailViewModel router = CreateVm(
            ControllerConnectionState.Connected,
            selectVrrpNode: false);
        Assert.False(router.IsVrrpNode);
        Assert.False(router.ValidateVrrpPairCommand.CanExecute(null));
        Assert.False(router.CaptureAllMembersAndValidateCommand.CanExecute(null));
    }

    [Fact]
    public void Ac5MainWindowBindsNodeVrrpValidateCaptureAndFindings()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Node.ValidateVrrpPairCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.CaptureAllMembersAndValidateCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.VrrpPairFindings", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.VrrpPairStatusText", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.VrrpMembers", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.IsVrrpNode", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostAndProtoContractRemainPresentForInventoryVrrp()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/InventoryGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/InventoryGrpcHostTests.cs"));
        Assert.Contains("InventoryLifecycleListGetRegisterValidateAndIdempotency", host, StringComparison.Ordinal);
        string proto = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/Protos/mfc/v1/inventory.proto"));
        Assert.Contains("ValidateVrrpPairConsistency", proto, StringComparison.Ordinal);
        string contract = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Contracts/InventoryProtoContractTests.cs"));
        Assert.Contains("ValidateVrrpPairConsistency", contract, StringComparison.Ordinal);
    }

    private static async Task WaitUntilIdleAsync(NodeDetailViewModel vm)
    {
        for (int i = 0; i < 100 && vm.IsBusy; i++)
        {
            await Task.Delay(10);
        }

        Assert.False(vm.IsBusy);
    }

    private static NodeDetailViewModel CreateVm(
        ControllerConnectionState state,
        bool selectVrrpNode,
        RecordingInventoryClient? client = null)
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceA = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceB = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
        FakeConnection connection = new(state);
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
                    DisplayName = selectVrrpNode ? "pair" : "core",
                    NodeKindText = selectVrrpNode ? "Vrrp" : "Router",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceA,
                            DisplayName = "member-a",
                            IsVrrpMember = selectVrrpNode,
                        },
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceB,
                            DisplayName = "member-b",
                            IsVrrpMember = selectVrrpNode,
                        },
                    ],
                },
            ],
        });
        inventory.Roots.Add(site);
        inventory.SelectedNode = site.Children[0];
        return new NodeDetailViewModel(
            inventory,
            new ZonesViewModel(new StubZones(), connection, inventory),
            new OnboardingViewModel(new StubOnboarding(), connection, inventory),
            client ?? new RecordingInventoryClient(),
            new StubSnapshotClient(),
            connection);
    }

    private static string ReadSource(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class RecordingInventoryClient : IInventoryTreeClient
    {
        public VrrpPairConsistencyReport Report { get; init; } = new() { Passed = true };

        public int ValidateVrrpCalls { get; private set; }

        public Task<IReadOnlyList<Site>> ListAllSitesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Site>>([]);

        public Task<IReadOnlyList<Node>> ListAllNodesAsync(Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Node>>([]);

        public Task<NodeDetails> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
            => Task.FromResult(new NodeDetails());

        public Task<NodeWorkflow> GetNodeWorkflowAsync(Guid nodeId, CancellationToken cancellationToken = default)
            => Task.FromResult(new NodeWorkflow());

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
        {
            ValidateVrrpCalls++;
            return Task.FromResult(Report);
        }
    }

    private sealed class StubSnapshotClient : ISnapshotViewerClient
    {
        public Task<StartCaptureResponse> StartCaptureAsync(
            Guid deviceId, Guid idempotencyKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StartCaptureResponse> StartNodeCaptureAsync(
            Guid nodeId, Guid idempotencyKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<CaptureProgress> WatchCaptureAsync(
            Guid operationId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<IReadOnlyList<SnapshotSummary>> ListCapturesAsync(
            Guid deviceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SnapshotSummary>>([]);

        public Task<SnapshotSummary> GetSummaryAsync(Guid captureId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SnapshotRecord>> GetAllSectionRecordsAsync(
            Guid captureId, string sectionId, DiffDomain domain, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SnapshotRecord>>([]);

        public Task<DiffPage> CompareSnapshotsAsync(
            Guid leftCaptureId, Guid rightCaptureId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeConnection(ControllerConnectionState state) : IControllerConnectionService
    {
        public GrpcChannel? Channel => null;

        public ControllerConnectionState State { get; } = state;

        public string? LastError => null;

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
        public Task<IReadOnlyList<ZoneDefinitionListItem>> ListZonesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZoneDefinitionListItem>>([]);

        public Task<ZoneDefinitionListItem> CreateCompanyZoneAsync(
            string key, string name, string? description, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteZoneAsync(ZoneDefinitionListItem zone, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<NodeZoneBindingListItem>> ListBindingsAsync(
            Guid nodeId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NodeZoneBindingListItem>>([]);

        public Task<NodeZoneBindingListItem> UpsertBindingAsync(
            Guid nodeId,
            Guid zoneId,
            NodeZoneBindingKind kind,
            IReadOnlyList<string> values,
            ulong? expectedRowVersion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteBindingAsync(NodeZoneBindingListItem binding, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForNodeAsync(
            Guid nodeId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZoneResolveResultListItem>>([]);

        public Task<ZoneDefinitionListItem> UpdateZoneAsync(
            ZoneDefinitionListItem zone,
            string name,
            string? description,
            bool resetDescription,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForDeviceAsync(
            Guid deviceId, CancellationToken cancellationToken = default)
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
            Guid planId, Sha256 planHash, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<OnboardingProgress> WatchAsync(
            Guid operationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingOperationSummary> RollbackAsync(
            Guid operationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingRecoveryStatus> GetRecoveryStatusAsync(
            Guid nodeId, Guid? operationId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
