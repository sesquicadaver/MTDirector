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
        Assert.Equal(deviceId.ToString("D"), vm.SafetyDeviceIdText);
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

    [Fact]
    public async Task RefreshCatalogFillsCatalogFromListPolicies()
    {
        Guid policyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0001");
        Guid revisionId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        PolicyCatalogListItem row = CatalogRow(policyId, revisionId, "lab-baseline");
        StubPolicyPanel panel = new()
        {
            DraftState = EmptyDraft(revisionId, policyId),
            CatalogItems = [row],
        };
        using PoliciesViewModel vm = new(panel, connection, inventory);

        await vm.RefreshCatalogCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.ListCatalogCalls);
        PolicyCatalogListItem listed = Assert.Single(vm.Catalog);
        Assert.Equal("lab-baseline", listed.Name);
        Assert.Equal(revisionId, listed.LatestRevisionId);
        Assert.Contains("lab-baseline", listed.SummaryLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshSafetyAnalysisBindsControllerHashesAndFindings()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        StubPolicyPanel panel = new()
        {
            SafetyResult = new PolicySafetyAnalysisPanelResult
            {
                DeviceId = deviceId,
                CaptureId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                ManagementPathContextHashHex = new string('a', 64),
                FastTrackContextHashHex = new string('b', 64),
                BlocksManagementPath = true,
                AllowsSafeFastTrack = false,
                RequiresAcceptFallback = false,
                RiskFloor = string.Empty,
                ManagementPathFindingLines = ["API_SSL_DISABLED [BLOCKER] api-ssl is disabled"],
                FastTrackFindingLines = [],
                WitnessLines = ["API_SSL_DISABLED: Ipv4 Input 192.0.2.1->192.0.2.10 dport=8729"],
                SystemTestLines = ["SYSTEM Input expected=ACCEPT"],
            },
        };
        using PoliciesViewModel vm = new(panel, connection, inventory)
        {
            SafetyDeviceIdText = deviceId.ToString("D"),
            ControllerSourcePrefixesText = "192.0.2.0/24",
        };

        await vm.RefreshSafetyAnalysisCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.SafetyAnalysisCalls);
        Assert.Equal(new string('a', 64), vm.ManagementPathContextHashText);
        Assert.Equal(new string('b', 64), vm.FastTrackContextHashText);
        Assert.Contains("blocks_management_path=True", vm.SafetyFlagsText, StringComparison.Ordinal);
        Assert.Contains("API_SSL_DISABLED", Assert.Single(vm.ManagementPathFindingLines), StringComparison.Ordinal);
        Assert.Contains("192.0.2.10", Assert.Single(vm.SafetyWitnessLines), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectingCatalogItemLoadsRevisionRulesAndObjects()
    {
        Guid policyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeee0001");
        Guid revisionId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");
        Guid ruleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid addressId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        PolicyCatalogListItem row = CatalogRow(policyId, revisionId, "lab-baseline");
        StubPolicyPanel panel = new()
        {
            DraftState = EmptyDraft(
                revisionId,
                policyId,
                [
                    new PolicyRuleListItem
                    {
                        Id = ruleId,
                        Family = IpAddressFamily.Ipv4,
                        Chain = PolicyFilterChain.Forward,
                        Stage = PolicyPipelineStage.CompanyAllow,
                        FamilyText = "Ipv4",
                        ChainText = "Forward",
                        StageText = "CompanyAllow",
                        Ordinal = 0,
                        Enabled = true,
                        Effect = PolicyRuleEffect.Accept,
                        EffectText = "Accept",
                        Description = "allow-lan",
                        WarningLines = [],
                    },
                ],
                [
                    new PolicyAddressObjectListItem
                    {
                        Id = addressId,
                        Name = "lan",
                        FamilyText = "Ipv4",
                        EntriesText = "10.0.0.0/8",
                    },
                ]),
            CatalogItems = [row],
        };
        using PoliciesViewModel vm = new(panel, connection, inventory);
        await vm.RefreshCatalogCommand.ExecuteAsync(null);

        vm.SelectedCatalogItem = row;
        await WaitUntil(() => panel.LoadRevisionCalls > 0);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.LoadRevisionCalls);
        Assert.Equal(revisionId, panel.LastLoadedRevisionId);
        Assert.Equal(revisionId.ToString("D"), vm.RevisionIdText);
        Assert.Equal("allow-lan", Assert.Single(vm.Rules).Description);
        Assert.Equal("lan", Assert.Single(vm.AddressObjects).Name);
    }

    [Fact]
    public async Task UpdateRuleCommandSendsSelectedRuleAndFormFields()
    {
        Guid revisionId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");
        Guid ruleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        RecordingPolicyPanel panel = new() { DraftState = EmptyDraft(revisionId) };
        using PoliciesViewModel vm = new(panel, connection, inventory)
        {
            DraftNameText = "lab-baseline",
        };
        await vm.CreateDraftCommand.ExecuteAsync(null);

        PolicyRuleListItem rule = new()
        {
            Id = ruleId,
            Family = IpAddressFamily.Ipv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            FamilyText = "Ipv4",
            ChainText = "Forward",
            StageText = "CompanyAllow",
            Ordinal = 2,
            Enabled = true,
            Effect = PolicyRuleEffect.Accept,
            EffectText = "Accept",
            Description = "allow-lan",
            WarningLines = [],
        };
        vm.Rules.Add(rule);
        vm.SelectedRule = rule;
        vm.RuleDescriptionText = "allow-lan-updated";
        vm.SelectedEffect = PolicyRuleEffect.Drop;

        await vm.UpdateRuleCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.UpdateCalls);
        Assert.Equal(ruleId, panel.LastUpdateRuleId);
        Assert.Equal(2u, panel.LastUpdateOrdinal);
        Assert.True(panel.LastUpdateEnabled);
        Assert.Equal(PolicyRuleEffect.Drop, panel.LastUpdateEffect);
        Assert.Equal("allow-lan-updated", panel.LastUpdateDescription);
        Assert.Equal("allow-lan-updated", Assert.Single(vm.Rules).Description);
    }

    [Fact]
    public async Task DeleteRuleCommandRemovesSelectedRule()
    {
        Guid revisionId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");
        Guid ruleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        RecordingPolicyPanel panel = new() { DraftState = EmptyDraft(revisionId) };
        using PoliciesViewModel vm = new(panel, connection, inventory)
        {
            DraftNameText = "lab-baseline",
        };
        await vm.CreateDraftCommand.ExecuteAsync(null);
        vm.SelectedRule = new PolicyRuleListItem
        {
            Id = ruleId,
            Family = IpAddressFamily.Ipv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            FamilyText = "Ipv4",
            ChainText = "Forward",
            StageText = "CompanyAllow",
            Ordinal = 0,
            Enabled = true,
            Effect = PolicyRuleEffect.Accept,
            EffectText = "Accept",
            Description = "allow-lan",
            WarningLines = [],
        };

        await vm.DeleteRuleCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.DeleteCalls);
        Assert.Equal(ruleId, panel.LastDeleteRuleId);
        Assert.Null(vm.SelectedRule);
        Assert.Empty(vm.Rules);
    }

    [Fact]
    public async Task AcknowledgeWarningCommandSendsHashedFinding()
    {
        Guid revisionId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        RecordingPolicyPanel panel = new() { DraftState = EmptyDraft(revisionId) };
        using PoliciesViewModel vm = new(panel, connection, inventory)
        {
            DraftNameText = "lab-baseline",
        };
        await vm.CreateDraftCommand.ExecuteAsync(null);
        vm.Findings.Add(new PolicyFindingListItem { SummaryLine = "empty selector" });

        await vm.RecordAnalysisCommand.ExecuteAsync(null);
        Assert.Null(vm.ErrorText);
        Assert.NotNull(vm.SelectedFinding);
        Assert.True(vm.SelectedFinding!.HasWarningHash);

        await vm.AcknowledgeWarningCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.AckCalls);
        Assert.Equal(panel.LastRecordedRunId, panel.LastAckRunId);
        Assert.Equal(vm.SelectedFinding.WarningHash, panel.LastAckWarningHash);
    }

    [Fact]
    public async Task CompileCommandRequiresNodeCapabilityHashAndRecordedRun()
    {
        Guid revisionId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");
        Guid nodeId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid deviceId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        RecordingPolicyPanel panel = new()
        {
            DraftState = EmptyDraft(revisionId),
            CompileDeviceId = deviceId,
        };
        using PoliciesViewModel vm = new(panel, connection, inventory)
        {
            DraftNameText = "lab-baseline",
        };
        await vm.CreateDraftCommand.ExecuteAsync(null);

        await vm.CompileCommand.ExecuteAsync(null);
        Assert.Contains("Select a Node", vm.ErrorText, StringComparison.Ordinal);

        vm.ComposeNodeIdText = nodeId.ToString("D");
        await vm.CompileCommand.ExecuteAsync(null);
        Assert.Contains("Record an analysis run", vm.ErrorText, StringComparison.Ordinal);

        await vm.RecordAnalysisCommand.ExecuteAsync(null);
        vm.CompileCapabilityHashText = "not-a-hash";
        await vm.CompileCommand.ExecuteAsync(null);
        Assert.Contains("64 hexadecimal", vm.ErrorText, StringComparison.Ordinal);

        vm.CompileCapabilityHashText = new string('b', 64);
        await vm.CompileCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, panel.CompileCalls);
        Assert.Equal(nodeId, panel.LastCompileNodeId);
        Assert.Equal(Convert.FromHexString(new string('b', 64)), panel.LastCompileCapabilityHash);
        Assert.Equal($"logical_effective={new string('c', 64)}", vm.CompileArtifactLines[0]);
        Assert.Contains($"device={deviceId:D}", vm.CompileArtifactLines[1], StringComparison.Ordinal);
    }

    private static PolicyCatalogListItem CatalogRow(Guid policyId, Guid revisionId, string name)
        => new()
        {
            PolicyId = policyId,
            Name = name,
            Kind = PolicyKind.CompanyBaseline,
            KindText = "CompanyBaseline",
            LatestRevisionId = revisionId,
            LatestRevisionNumber = 1,
            LatestRevisionState = PolicyRevisionState.Draft,
            StateText = "Draft",
        };

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }

    private static PolicyRevisionPanelState EmptyDraft(
        Guid revisionId,
        Guid? policyId = null,
        IReadOnlyList<PolicyRuleListItem>? rules = null,
        IReadOnlyList<PolicyAddressObjectListItem>? addresses = null)
        => new()
        {
            RevisionId = revisionId,
            PolicyId = policyId ?? Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000"),
            State = PolicyRevisionState.Draft,
            StateText = "Draft",
            Kind = PolicyKind.CompanyBaseline,
            KindText = "CompanyBaseline",
            ContentHash = new byte[32],
            ContentHashHex = new string('a', 64),
            IsReadOnly = false,
            TestsJson = "[]",
            Rules = rules ?? [],
            AddressObjects = addresses ?? [],
            ServiceObjects = [],
            ChainContracts = [],
            RevisionWarnings = [],
        };

    private sealed class StubPolicyPanel : IPolicyPanelService
    {
        public PolicyRevisionPanelState? DraftState { get; init; }

        public IReadOnlyList<PolicyCatalogListItem> CatalogItems { get; init; } = [];

        public int CreateDraftCalls { get; private set; }

        public int ListCatalogCalls { get; private set; }

        public int LoadRevisionCalls { get; private set; }

        public Guid LastLoadedRevisionId { get; private set; }

        public Task<PolicyRevisionPanelState> CreateDraftAsync(
            string name,
            PolicyKind kind = PolicyKind.CompanyBaseline,
            CancellationToken cancellationToken = default)
        {
            CreateDraftCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DraftState ?? throw new InvalidOperationException("DraftState not set."));
        }

        public Task<IReadOnlyList<PolicyCatalogListItem>> ListCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            ListCatalogCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CatalogItems);
        }

        public Task<PolicyRevisionPanelState> LoadRevisionAsync(
            Guid revisionId,
            CancellationToken cancellationToken = default)
        {
            LoadRevisionCalls++;
            LastLoadedRevisionId = revisionId;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DraftState ?? throw new InvalidOperationException("DraftState not set."));
        }

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

        public PolicySafetyAnalysisPanelResult? SafetyResult { get; init; }

        public int SafetyAnalysisCalls { get; private set; }

        public Task<PolicySafetyAnalysisPanelResult> GetDevicePolicySafetyAnalysisAsync(
            Guid deviceId,
            Guid? revisionId,
            IReadOnlyList<string> controllerSourcePrefixes,
            CancellationToken cancellationToken = default)
        {
            SafetyAnalysisCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SafetyResult ?? throw new InvalidOperationException("SafetyResult not set."));
        }
    }

    private sealed class RecordingPolicyPanel : IPolicyPanelService
    {
        public PolicyRevisionPanelState? DraftState { get; init; }

        public Guid CompileDeviceId { get; init; } = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

        public int UpdateCalls { get; private set; }

        public int DeleteCalls { get; private set; }

        public int AckCalls { get; private set; }

        public int CompileCalls { get; private set; }

        public Guid LastUpdateRuleId { get; private set; }

        public uint LastUpdateOrdinal { get; private set; }

        public bool LastUpdateEnabled { get; private set; }

        public PolicyRuleEffect LastUpdateEffect { get; private set; }

        public string? LastUpdateDescription { get; private set; }

        public Guid LastDeleteRuleId { get; private set; }

        public Guid LastRecordedRunId { get; private set; } = Guid.Parse("44444444-5555-6666-7777-888888888888");

        public Guid LastAckRunId { get; private set; }

        public byte[]? LastAckWarningHash { get; private set; }

        public Guid LastCompileNodeId { get; private set; }

        public byte[]? LastCompileCapabilityHash { get; private set; }

        public Task<PolicyRevisionPanelState> CreateDraftAsync(
            string name,
            PolicyKind kind = PolicyKind.CompanyBaseline,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DraftState ?? throw new InvalidOperationException("DraftState not set."));
        }

        public Task<IReadOnlyList<PolicyCatalogListItem>> ListCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<PolicyCatalogListItem>>([]);
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
        {
            UpdateCalls++;
            LastUpdateRuleId = ruleId;
            LastUpdateOrdinal = ordinal;
            LastUpdateEnabled = enabled;
            LastUpdateEffect = effectKind;
            LastUpdateDescription = description;
            PolicyRevisionPanelState draft = DraftState ?? throw new InvalidOperationException("DraftState not set.");
            return Task.FromResult(CloneDraft(draft, [
                new PolicyRuleListItem
                {
                    Id = ruleId,
                    Family = family,
                    Chain = chain,
                    Stage = stage,
                    FamilyText = family.ToString(),
                    ChainText = chain.ToString(),
                    StageText = stage.ToString(),
                    Ordinal = ordinal,
                    Enabled = enabled,
                    Effect = effectKind,
                    EffectText = effectKind.ToString(),
                    Description = description,
                    WarningLines = [],
                },
            ]));
        }

        public Task<PolicyRevisionPanelState> DeleteRuleAsync(
            Guid revisionId,
            Guid ruleId,
            byte[] expectedContentHash,
            CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            LastDeleteRuleId = ruleId;
            PolicyRevisionPanelState draft = DraftState ?? throw new InvalidOperationException("DraftState not set.");
            return Task.FromResult(CloneDraft(draft, []));
        }

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
        {
            List<PolicyFindingListItem> ackable = [];
            if (composeFindings is not null)
            {
                foreach (PolicyFindingListItem item in composeFindings)
                {
                    string code = string.IsNullOrWhiteSpace(item.Code) ? "DESKTOP_COMPOSE_FINDING" : item.Code.Trim();
                    string target = string.IsNullOrWhiteSpace(item.Target) ? "compose" : item.Target.Trim();
                    string message = string.IsNullOrWhiteSpace(item.Message) ? item.SummaryLine : item.Message.Trim();
                    ackable.Add(new PolicyFindingListItem
                    {
                        SummaryLine = $"{code}: {message}",
                        Code = code,
                        Target = target,
                        Message = message,
                        WarningHash = PolicyPanelService.HashWarning(code, target, message),
                    });
                }
            }

            return Task.FromResult(new PolicyAnalysisRunListItem
            {
                Id = LastRecordedRunId,
                RiskLevel = riskLevel,
                EffectiveRiskLevel = "LOW",
                BundleHash = new byte[32],
                DependencyFingerprint = Enumerable.Repeat((byte)9, 32).ToArray(),
                AckableFindings = ackable,
            });
        }

        public Task<PolicyAnalysisRunListItem> AcknowledgeWarningAsync(
            Guid analysisRunId,
            byte[] warningHash,
            CancellationToken cancellationToken = default)
        {
            AckCalls++;
            LastAckRunId = analysisRunId;
            LastAckWarningHash = warningHash;
            return Task.FromResult(new PolicyAnalysisRunListItem
            {
                Id = analysisRunId,
                RiskLevel = "HIGH",
                EffectiveRiskLevel = "MEDIUM",
                BundleHash = new byte[32],
                DependencyFingerprint = Enumerable.Repeat((byte)9, 32).ToArray(),
            });
        }

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
        {
            CompileCalls++;
            LastCompileNodeId = nodeId;
            LastCompileCapabilityHash = currentCapabilityHash;
            return Task.FromResult(new PolicyCompilePanelResult
            {
                NodeId = nodeId,
                LogicalEffectiveHashHex = new string('c', 64),
                ArtifactLines =
                [
                    $"device={CompileDeviceId:D} artifact=art-1 rules=2 new=True",
                ],
            });
        }

        public Task<PolicySafetyAnalysisPanelResult> GetDevicePolicySafetyAnalysisAsync(
            Guid deviceId,
            Guid? revisionId,
            IReadOnlyList<string> controllerSourcePrefixes,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static PolicyRevisionPanelState CloneDraft(
            PolicyRevisionPanelState draft,
            IReadOnlyList<PolicyRuleListItem> rules)
            => new()
            {
                RevisionId = draft.RevisionId,
                PolicyId = draft.PolicyId,
                State = draft.State,
                StateText = draft.StateText,
                Kind = draft.Kind,
                KindText = draft.KindText,
                ContentHash = draft.ContentHash,
                ContentHashHex = draft.ContentHashHex,
                IsReadOnly = draft.IsReadOnly,
                TestsJson = draft.TestsJson,
                Rules = rules,
                AddressObjects = draft.AddressObjects,
                ServiceObjects = draft.ServiceObjects,
                ChainContracts = draft.ChainContracts,
                RevisionWarnings = draft.RevisionWarnings,
            };
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
