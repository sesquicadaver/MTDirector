using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-POLICY-01 / W7-79: Desktop Policies panel Living Spec vs PolicyGrpcHost.</summary>
public sealed class DesktopPoliciesLivingSpecTests
{
    private static readonly string[] ExpectedWireMethods =
    [
        "AcknowledgeWarning",
        "ActivateDesiredBinding",
        "AddRule",
        "ApproveRevision",
        "CompileNodeFilterArtifacts",
        "ComposeEffectivePolicy",
        "CreateDraftPolicy",
        "DeleteRule",
        "DiffPolicyRevisions",
        "ExpireExceptionBinding",
        "GetDevicePolicySafetyAnalysis",
        "GetPolicyRevision",
        "GetRule",
        "ListPolicies",
        "ListRules",
        "RecordAnalysisRun",
        "ReorderRules",
        "ReplaceChainContracts",
        "ReplacePolicyTests",
        "SubmitRevisionForReview",
        "UpdateExceptionMetadata",
        "UpdateRule",
        "UpsertAddressObject",
        "UpsertServiceObject",
        "ValidateRevision",
    ];

    private static readonly string[] ExpectedDesktopClientRpcs =
    [
        "AcknowledgeWarning",
        "ActivateDesiredBinding",
        "AddRule",
        "ApproveRevision",
        "CompileNodeFilterArtifacts",
        "ComposeEffectivePolicy",
        "CreateDraftPolicy",
        "DeleteRule",
        "DiffPolicyRevisions",
        "GetDevicePolicySafetyAnalysis",
        "GetPolicyRevision",
        "ListPolicies",
        "ListRules",
        "RecordAnalysisRun",
        "ReorderRules",
        "ReplaceChainContracts",
        "ReplacePolicyTests",
        "SubmitRevisionForReview",
        "UpdateRule",
        "UpsertAddressObject",
        "UpsertServiceObject",
        "ValidateRevision",
    ];

    [Fact]
    public void Ac1WireAndDesktopClientExposeAuthoringReviewAndSafetyRpcs()
    {
        string[] methods = PolicyService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedWireMethods, methods);

        Type client = typeof(IPolicyServiceClient);
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.ListPoliciesAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.CreateDraftPolicyAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.GetPolicyRevisionAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.ListRulesAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.AddRuleAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.UpdateRuleAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.DeleteRuleAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.ReorderRulesAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.ValidateRevisionAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.UpsertAddressObjectAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.UpsertServiceObjectAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.ReplaceChainContractsAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.ReplacePolicyTestsAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.DiffPolicyRevisionsAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.ComposeEffectivePolicyAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.SubmitRevisionForReviewAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.RecordAnalysisRunAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.AcknowledgeWarningAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.ApproveRevisionAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.ActivateDesiredBindingAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.CompileNodeFilterArtifactsAsync)));
        Assert.NotNull(client.GetMethod(nameof(IPolicyServiceClient.GetDevicePolicySafetyAnalysisAsync)));

        string source = ReadSource("src/Mfc.Desktop/Services/GrpcPolicyServiceClient.cs");
        foreach (string rpc in ExpectedDesktopClientRpcs)
        {
            Assert.Contains(rpc + "Async", source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("GetRuleAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateExceptionMetadataAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExpireExceptionBindingAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ViewModelExposesCatalogAuthoringReviewBindAndFailClosedDeploy()
    {
        using PoliciesViewModel vm = CreateVm(
            new FakePolicyPanel(),
            ControllerConnectionState.Connected);

        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.RefreshCatalogCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.LoadCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.CreateDraftCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.AddRuleCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.UpdateRuleCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.DeleteRuleCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.ValidateCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.SubmitCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.ApproveCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.BindCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.ComposeCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.DiffCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.RefreshSafetyAnalysisCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.DeployCommand)));
        Assert.False(vm.DeployCommand.CanExecute(null));
        Assert.Contains(PolicyFilterChain.Forward, vm.Chains);
        Assert.Contains(PolicyRuleEffect.Accept, vm.Effects);
    }

    [Fact]
    public async Task Ac3RefreshCatalogLoadsPoliciesWhenConnected()
    {
        Guid policyId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid revisionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        FakePolicyPanel panel = new()
        {
            Catalog =
            [
                new PolicyCatalogListItem
                {
                    PolicyId = policyId,
                    Name = "baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    KindText = nameof(PolicyKind.CompanyBaseline),
                    LatestRevisionId = revisionId,
                    LatestRevisionNumber = 1,
                    LatestRevisionState = PolicyRevisionState.Draft,
                    StateText = nameof(PolicyRevisionState.Draft),
                },
            ],
        };

        using PoliciesViewModel vm = CreateVm(panel, ControllerConnectionState.Connected);

        await vm.RefreshCatalogCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.ListCatalogCalls);
        Assert.Equal(policyId, Assert.Single(vm.Catalog).PolicyId);
        Assert.False(vm.HasEmptyCatalog);
    }

    [Fact]
    public async Task Ac4CatalogRequiresConnectedControllerAndDeployStaysFailClosed()
    {
        FakePolicyPanel disconnectedPanel = new();
        using PoliciesViewModel disconnected = CreateVm(
            disconnectedPanel,
            ControllerConnectionState.Disconnected);

        await disconnected.RefreshCatalogCommand.ExecuteAsync(null);

        Assert.Equal(0, disconnectedPanel.ListCatalogCalls);
        Assert.Contains("Connect to Controller", disconnected.ErrorText, StringComparison.Ordinal);
        Assert.False(disconnected.DeployCommand.CanExecute(null));

        FakePolicyPanel connectedPanel = new();
        using PoliciesViewModel connected = CreateVm(
            connectedPanel,
            ControllerConnectionState.Connected);

        Assert.False(connected.DeployCommand.CanExecute(null));
        await connected.DeployCommand.ExecuteAsync(null);
        Assert.Contains("Deploy from Policies stays blocked", connected.ErrorText ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5MainWindowBindsPoliciesCatalogAuthoringReviewAndSafety()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Policy authoring / review / binding", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.RefreshCatalogCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.Catalog", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.LoadCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CreateDraftCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ValidateCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SubmitCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ApproveCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.BindCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DeployCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ComposeCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.RefreshSafetyAnalysisCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.AddRuleCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.UpdateRuleCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DeleteRuleCommand", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/PolicyGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/PolicyGrpcHostTests.cs"));
        Assert.Contains("C2CreateDraftAddListUpdateDeleteRulesWithContentHashCas", host, StringComparison.Ordinal);
        Assert.Contains("ListPoliciesReturnsCreatedDraftsWithLatestRevision", host, StringComparison.Ordinal);
        Assert.Contains("ApprovalAndDesiredBindingAreSeparateAndDoNotDeploy", host, StringComparison.Ordinal);
        Assert.Contains("GetDevicePolicySafetyAnalysisUnknownDeviceReturnsNotFound", host, StringComparison.Ordinal);
    }

    private static PoliciesViewModel CreateVm(
        IPolicyPanelService panel,
        ControllerConnectionState state)
    {
        FakeConnection connection = new(state);
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        return new PoliciesViewModel(panel, connection, inventory);
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

    private sealed class FakePolicyPanel : IPolicyPanelService
    {
        public IReadOnlyList<PolicyCatalogListItem> Catalog { get; init; } = [];

        public int ListCatalogCalls { get; private set; }

        public Task<IReadOnlyList<PolicyCatalogListItem>> ListCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            ListCatalogCalls++;
            return Task.FromResult(Catalog);
        }

        public Task<PolicyRevisionPanelState> LoadRevisionAsync(
            Guid revisionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> CreateDraftAsync(
            string name,
            PolicyKind kind = PolicyKind.CompanyBaseline,
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

        public Task<PolicyRevisionPanelState> UpdateRuleAsync(
            Guid revisionId,
            Guid ruleId,
            byte[] expectedContentHash,
            IpAddressFamily family,
            PolicyFilterChain chain,
            PolicyPipelineStage stage,
            uint ordinal,
            bool enabled,
            PolicyRuleEffect effectKind,
            string description,
            TrafficPredicate? predicate,
            LogSpecification? logging = null,
            bool exceptionEligible = false,
            RejectMode rejectMode = RejectMode.Unspecified,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicyRevisionPanelState> DeleteRuleAsync(
            Guid revisionId,
            Guid ruleId,
            byte[] expectedContentHash,
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

        public Task<PolicyAnalysisRunListItem> AcknowledgeWarningAsync(
            Guid analysisRunId,
            byte[] warningHash,
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

        public Task<PolicyCompilePanelResult> CompileNodeFilterArtifactsAsync(
            Guid nodeId,
            Guid analysisRunId,
            byte[] currentDependencyFingerprint,
            byte[] currentCapabilityHash,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PolicySafetyAnalysisPanelResult> GetDevicePolicySafetyAnalysisAsync(
            Guid deviceId,
            Guid? revisionId,
            IReadOnlyList<string> controllerSourcePrefixes,
            CancellationToken cancellationToken = default)
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
}
