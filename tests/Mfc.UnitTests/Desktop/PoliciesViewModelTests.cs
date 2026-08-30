using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W1.3: Compose node default from inventory selection; Create draft fills revision id.</summary>
public sealed class PoliciesViewModelTests
{
    [Fact]
    public void SelectingInventoryNodeFillsComposeNodeId()
    {
        Guid nodeId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using PoliciesViewModel vm = new(new StubPolicyPanel(), connection, inventory);

        inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Node,
            Id = nodeId,
            DisplayName = "core",
        });

        Assert.Equal(nodeId.ToString("D"), vm.ComposeNodeIdText);
    }

    [Fact]
    public void SelectingDeviceFillsComposeNodeIdFromParent()
    {
        Guid nodeId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        using PoliciesViewModel vm = new(new StubPolicyPanel(), connection, inventory);

        inventory.SelectedNode = new InventoryNodeViewModel(
            new InventoryTreeItem
            {
                Kind = InventoryTreeKind.Device,
                Id = deviceId,
                DisplayName = "chr-seed",
            },
            parentId: nodeId);

        Assert.Equal(nodeId.ToString("D"), vm.ComposeNodeIdText);
    }

    [Fact]
    public async Task CreateDraftWritesRevisionIdWithoutSeparateLoad()
    {
        Guid revisionId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        StubPolicyPanel panel = new() { DraftState = EmptyDraft(revisionId) };
        using PoliciesViewModel vm = new(panel, connection, inventory)
        {
            DraftNameText = "lab-baseline",
        };

        await vm.CreateDraftCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(revisionId.ToString("D"), vm.RevisionIdText);
        Assert.Equal("Draft", vm.StateText);
        Assert.Equal(1, panel.CreateDraftCalls);
    }

    private static PolicyRevisionPanelState EmptyDraft(Guid revisionId)
        => new()
        {
            RevisionId = revisionId,
            PolicyId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000"),
            State = PolicyRevisionState.Draft,
            StateText = "Draft",
            Kind = PolicyKind.CompanyBaseline,
            KindText = "CompanyBaseline",
            ContentHash = new byte[32],
            ContentHashHex = new string('a', 64),
            IsReadOnly = false,
            TestsJson = "[]",
            Rules = [],
            AddressObjects = [],
            ServiceObjects = [],
            ChainContracts = [],
            RevisionWarnings = [],
        };

    private sealed class StubPolicyPanel : IPolicyPanelService
    {
        public PolicyRevisionPanelState? DraftState { get; init; }

        public int CreateDraftCalls { get; private set; }

        public Task<PolicyRevisionPanelState> CreateDraftAsync(
            string name,
            PolicyKind kind = PolicyKind.CompanyBaseline,
            CancellationToken cancellationToken = default)
        {
            CreateDraftCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DraftState ?? throw new InvalidOperationException("DraftState not set."));
        }

        public Task<PolicyRevisionPanelState> LoadRevisionAsync(
            Guid revisionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> ValidateAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> SubmitForReviewAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> AddRuleAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            IpAddressFamily family,
            PolicyFilterChain chain,
            PolicyPipelineStage stage,
            PolicyRuleEffect effectKind,
            string description,
            TrafficPredicate? predicate,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> ReorderRulesInStageAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            IpAddressFamily family,
            PolicyFilterChain chain,
            PolicyPipelineStage stage,
            IReadOnlyList<Guid> orderedRuleIds,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> UpsertAddressObjectAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            string name,
            IpAddressFamily family,
            string entriesText,
            Guid? objectId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> UpsertTcpServiceObjectAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            string name,
            uint tcpPort,
            Guid? objectId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> ReplaceChainContractsAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            IpAddressFamily family,
            PolicyFilterChain chain,
            string disposition,
            RejectMode? rejectMode = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> ReplaceTestsAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            string testsJson,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyDiffPanelResult> DiffAsync(
            Guid beforeRevisionId,
            Guid afterRevisionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyComposePanelResult> ComposeAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyAnalysisRunListItem> RecordAnalysisRunAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            byte[] logicalEffectiveHash,
            string riskLevel,
            IReadOnlyList<PolicyFindingListItem>? composeFindings = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ApproveAsync(
            Guid revisionId,
            Guid analysisRunId,
            byte[] expectedContentHash,
            byte[] expectedBundleHash,
            byte[] currentDependencyFingerprint,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task BindAsync(
            Guid revisionId,
            Guid analysisRunId,
            byte[] expectedContentHash,
            byte[] currentDependencyFingerprint,
            CancellationToken cancellationToken = default)
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
}
