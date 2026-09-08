using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-POLICY-02 / W7-96: Desktop Policy safety analysis Living Spec depth.</summary>
public sealed class DesktopPolicySafetyLivingSpecTests
{
    [Fact]
    public void Ac1WireAndDesktopClientExposeGetDevicePolicySafetyAnalysis()
    {
        Assert.Contains(
            PolicyService.Descriptor.Methods,
            static m => m.Name == "GetDevicePolicySafetyAnalysis");
        Assert.NotNull(
            typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.GetDevicePolicySafetyAnalysisAsync)));
        Assert.NotNull(
            typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.GetDevicePolicySafetyAnalysisAsync)));
        string source = ReadSource("src/Mfc.Desktop/Services/GrpcPolicyServiceClient.cs");
        Assert.Contains("GetDevicePolicySafetyAnalysisAsync", source, StringComparison.Ordinal);
        Assert.Contains("GetDevicePolicySafetyAnalysisRequest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ViewModelExposesRefreshSafetyAnalysisCommandAndResultSurface()
    {
        using PoliciesViewModel vm = CreateVm(new FakePolicyPanel(), ControllerConnectionState.Connected);

        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.RefreshSafetyAnalysisCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.SafetyDeviceIdText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.ControllerSourcePrefixesText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.ManagementPathContextHashText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.FastTrackContextHashText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.SafetyFlagsText)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.ManagementPathFindingLines)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.FastTrackFindingLines)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.SafetyWitnessLines)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(PoliciesViewModel.SafetySystemTestLines)));
    }

    [Fact]
    public async Task Ac3RefreshSafetyAnalysisBindsHashesFlagsFindingsAndWitnesses()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakePolicyPanel panel = new()
        {
            SafetyResult = new PolicySafetyAnalysisPanelResult
            {
                DeviceId = deviceId,
                CaptureId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                RevisionId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd"),
                ManagementPathContextHashHex = new string('a', 64),
                FastTrackContextHashHex = new string('b', 64),
                BlocksManagementPath = true,
                AllowsSafeFastTrack = false,
                RequiresAcceptFallback = true,
                RiskFloor = "HIGH",
                ManagementPathFindingLines = ["API_SSL_DISABLED [BLOCKER] api-ssl is disabled"],
                FastTrackFindingLines = ["FASTTRACK_UNSAFE [WARN] connection-mark missing"],
                WitnessLines = ["API_SSL_DISABLED: Ipv4 Input 192.0.2.1->192.0.2.10 dport=8729"],
                SystemTestLines = ["SYSTEM Input expected=ACCEPT"],
            },
        };

        using PoliciesViewModel vm = CreateVm(panel, ControllerConnectionState.Connected);
        vm.SafetyDeviceIdText = deviceId.ToString("D");
        vm.ControllerSourcePrefixesText = "192.0.2.0/24";
        vm.RevisionIdText = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd").ToString("D");

        Assert.True(vm.RefreshSafetyAnalysisCommand.CanExecute(null));
        await vm.RefreshSafetyAnalysisCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.SafetyAnalysisCalls);
        Assert.Equal(deviceId, panel.LastDeviceId);
        Assert.Equal(Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd"), panel.LastRevisionId);
        Assert.Equal(["192.0.2.0/24"], panel.LastPrefixes);
        Assert.Equal(new string('a', 64), vm.ManagementPathContextHashText);
        Assert.Equal(new string('b', 64), vm.FastTrackContextHashText);
        Assert.Contains("blocks_management_path=True", vm.SafetyFlagsText, StringComparison.Ordinal);
        Assert.Contains("allows_safe_fasttrack=False", vm.SafetyFlagsText, StringComparison.Ordinal);
        Assert.Contains("requires_accept_fallback=True", vm.SafetyFlagsText, StringComparison.Ordinal);
        Assert.Contains("risk_floor=HIGH", vm.SafetyFlagsText, StringComparison.Ordinal);
        Assert.Contains("API_SSL_DISABLED", Assert.Single(vm.ManagementPathFindingLines), StringComparison.Ordinal);
        Assert.Contains("FASTTRACK_UNSAFE", Assert.Single(vm.FastTrackFindingLines), StringComparison.Ordinal);
        Assert.Contains("192.0.2.10", Assert.Single(vm.SafetyWitnessLines), StringComparison.Ordinal);
        Assert.Contains("SYSTEM Input", Assert.Single(vm.SafetySystemTestLines), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac4SafetyAnalysisRequiresDevicePrefixesAndConnectedController()
    {
        FakePolicyPanel disconnectedPanel = new();
        using PoliciesViewModel disconnected = CreateVm(
            disconnectedPanel,
            ControllerConnectionState.Disconnected);
        disconnected.SafetyDeviceIdText = Guid.Parse("11111111-2222-3333-4444-555555555555").ToString("D");
        disconnected.ControllerSourcePrefixesText = "192.0.2.0/24";
        Assert.False(disconnected.RefreshSafetyAnalysisCommand.CanExecute(null));

        FakePolicyPanel noDevicePanel = new();
        using PoliciesViewModel noDevice = CreateVm(noDevicePanel, ControllerConnectionState.Connected);
        noDevice.ControllerSourcePrefixesText = "192.0.2.0/24";
        await noDevice.RefreshSafetyAnalysisCommand.ExecuteAsync(null);
        Assert.Equal(0, noDevicePanel.SafetyAnalysisCalls);
        Assert.Contains("Select a Device", noDevice.ErrorText, StringComparison.Ordinal);

        FakePolicyPanel noPrefixPanel = new();
        using PoliciesViewModel noPrefix = CreateVm(noPrefixPanel, ControllerConnectionState.Connected);
        noPrefix.SafetyDeviceIdText = Guid.Parse("11111111-2222-3333-4444-555555555555").ToString("D");
        await noPrefix.RefreshSafetyAnalysisCommand.ExecuteAsync(null);
        Assert.Equal(0, noPrefixPanel.SafetyAnalysisCalls);
        Assert.Contains("controller source CIDR", noPrefix.ErrorText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac5MainWindowBindsSafetyAnalysisInputsAndResults()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Policies.SafetyDeviceIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ControllerSourcePrefixesText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.RefreshSafetyAnalysisCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ManagementPathContextHashText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.FastTrackContextHashText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SafetyFlagsText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ManagementPathFindingLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.FastTrackFindingLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SafetyWitnessLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SafetySystemTestLines", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostAndProtoContractRemainPresentForGetDevicePolicySafetyAnalysis()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/PolicyGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/PolicyGrpcHostTests.cs"));
        Assert.Contains("GetDevicePolicySafetyAnalysisUnknownDeviceReturnsNotFound", host, StringComparison.Ordinal);
        string proto = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/Protos/mfc/v1/policy.proto"));
        Assert.Contains("GetDevicePolicySafetyAnalysis", proto, StringComparison.Ordinal);
        string contract = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Contracts/PolicyProtoContractTests.cs"));
        Assert.Contains("GetDevicePolicySafetyAnalysis", contract, StringComparison.Ordinal);
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
        public PolicySafetyAnalysisPanelResult? SafetyResult { get; init; }

        public int SafetyAnalysisCalls { get; private set; }

        public Guid LastDeviceId { get; private set; }

        public Guid? LastRevisionId { get; private set; }

        public IReadOnlyList<string> LastPrefixes { get; private set; } = [];

        public Task<IReadOnlyList<PolicyCatalogListItem>> ListCatalogAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PolicyCatalogListItem>>([]);

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
        {
            SafetyAnalysisCalls++;
            LastDeviceId = deviceId;
            LastRevisionId = revisionId;
            LastPrefixes = controllerSourcePrefixes.ToArray();
            return Task.FromResult(SafetyResult ?? throw new InvalidOperationException("SafetyResult required."));
        }
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
