using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Presentation row for a policy rule.</summary>
public sealed class PolicyRuleListItem
{
    public required Guid Id { get; init; }

    public required string FamilyText { get; init; }

    public required string ChainText { get; init; }

    public required string StageText { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required PolicyPipelineStage Stage { get; init; }

    public required uint Ordinal { get; init; }

    public required bool Enabled { get; init; }

    public required string EffectText { get; init; }

    public required PolicyRuleEffect Effect { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<string> WarningLines { get; init; }

    public string SummaryLine
    {
        get
        {
            string enabled = Enabled ? "on" : "off";
            string warnings = WarningLines.Count == 0
                ? string.Empty
                : " | " + string.Join("; ", WarningLines);
            return $"#{Ordinal} {FamilyText}/{ChainText}/{StageText} {EffectText} [{enabled}] {Description}{warnings}";
        }
    }
}

/// <summary>Presentation row for an address object.</summary>
public sealed class PolicyAddressObjectListItem
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string FamilyText { get; init; }

    public required string EntriesText { get; init; }

    public string SummaryLine => $"{Name} ({FamilyText}): {EntriesText}";
}

/// <summary>Presentation row for a service object.</summary>
public sealed class PolicyServiceObjectListItem
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string TermsText { get; init; }

    public string SummaryLine => $"{Name}: {TermsText}";
}

/// <summary>Presentation row for a chain contract.</summary>
public sealed class PolicyChainContractListItem
{
    public required string FamilyText { get; init; }

    public required string ChainText { get; init; }

    public required string Disposition { get; init; }

    public string? RejectModeText { get; init; }

    public string SummaryLine => string.IsNullOrWhiteSpace(RejectModeText)
        ? $"{FamilyText}/{ChainText} → {Disposition}"
        : $"{FamilyText}/{ChainText} → {Disposition} ({RejectModeText})";
}

/// <summary>Presentation row for a finding / warning / diff line.</summary>
public sealed class PolicyFindingListItem
{
    public required string SummaryLine { get; init; }

    public string? Code { get; init; }

    public string? Target { get; init; }

    public string? Message { get; init; }

    public byte[]? WarningHash { get; init; }

    public bool HasWarningHash => WarningHash is { Length: 32 };
}

/// <summary>Loaded revision snapshot for the Policies panel.</summary>
public sealed class PolicyRevisionPanelState
{
    public required Guid RevisionId { get; init; }

    public required Guid PolicyId { get; init; }

    public required PolicyRevisionState State { get; init; }

    public required string StateText { get; init; }

    public required PolicyKind Kind { get; init; }

    public required string KindText { get; init; }

    public required byte[] ContentHash { get; init; }

    public required string ContentHashHex { get; init; }

    public required bool IsReadOnly { get; init; }

    public required string TestsJson { get; init; }

    public required IReadOnlyList<PolicyRuleListItem> Rules { get; init; }

    public required IReadOnlyList<PolicyAddressObjectListItem> AddressObjects { get; init; }

    public required IReadOnlyList<PolicyServiceObjectListItem> ServiceObjects { get; init; }

    public required IReadOnlyList<PolicyChainContractListItem> ChainContracts { get; init; }

    public required IReadOnlyList<PolicyFindingListItem> RevisionWarnings { get; init; }
}

/// <summary>Result of semantic diff for review UI.</summary>
public sealed class PolicyDiffPanelResult
{
    public required string RiskLevel { get; init; }

    public required IReadOnlyList<PolicyFindingListItem> Lines { get; init; }
}

/// <summary>Result of compose findings for review UI.</summary>
public sealed class PolicyComposePanelResult
{
    public required byte[] LogicalEffectiveHash { get; init; }

    public required string LogicalEffectiveHashHex { get; init; }

    public required IReadOnlyList<PolicyFindingListItem> Findings { get; init; }
}

/// <summary>Recorded analysis-run summary for risk display / approve+bind.</summary>
public sealed class PolicyAnalysisRunListItem
{
    public required Guid Id { get; init; }

    public required string RiskLevel { get; init; }

    public required string EffectiveRiskLevel { get; init; }

    public required byte[] BundleHash { get; init; }

    public required byte[] DependencyFingerprint { get; init; }

    public IReadOnlyList<PolicyFindingListItem> AckableFindings { get; init; } = [];

    public string SummaryLine => $"run={Id:D} risk={RiskLevel} effective={EffectiveRiskLevel}";
}

/// <summary>Semantic compile summary (no RouterOS commands).</summary>
public sealed class PolicyCompilePanelResult
{
    public required Guid NodeId { get; init; }

    public required string LogicalEffectiveHashHex { get; init; }

    public required IReadOnlyList<string> ArtifactLines { get; init; }
}

/// <summary>Desktop policy panel orchestration over Contracts-only client (M2-18).</summary>
public interface IPolicyPanelService
{
    Task<PolicyRevisionPanelState> LoadRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> CreateDraftAsync(
        string name,
        PolicyKind kind = PolicyKind.CompanyBaseline,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> ValidateAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> SubmitForReviewAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> AddRuleAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        PolicyRuleEffect effectKind,
        string description,
        TrafficPredicate? predicate,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> UpdateRuleAsync(
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
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> DeleteRuleAsync(
        Guid revisionId,
        Guid ruleId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> ReorderRulesInStageAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        IReadOnlyList<Guid> orderedRuleIds,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> UpsertAddressObjectAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        string name,
        IpAddressFamily family,
        string entriesText,
        Guid? objectId = null,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> UpsertTcpServiceObjectAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        string name,
        uint tcpPort,
        Guid? objectId = null,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> ReplaceChainContractsAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        string disposition,
        RejectMode? rejectMode = null,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionPanelState> ReplaceTestsAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        string testsJson,
        CancellationToken cancellationToken = default);

    Task<PolicyDiffPanelResult> DiffAsync(
        Guid beforeRevisionId,
        Guid afterRevisionId,
        CancellationToken cancellationToken = default);

    Task<PolicyComposePanelResult> ComposeAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default);

    Task<PolicyAnalysisRunListItem> RecordAnalysisRunAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        byte[] logicalEffectiveHash,
        string riskLevel,
        IReadOnlyList<PolicyFindingListItem>? composeFindings = null,
        CancellationToken cancellationToken = default);

    Task<PolicyAnalysisRunListItem> AcknowledgeWarningAsync(
        Guid analysisRunId,
        byte[] warningHash,
        CancellationToken cancellationToken = default);

    Task ApproveAsync(
        Guid revisionId,
        Guid analysisRunId,
        byte[] expectedContentHash,
        byte[] expectedBundleHash,
        byte[] currentDependencyFingerprint,
        CancellationToken cancellationToken = default);

    Task BindAsync(
        Guid revisionId,
        Guid analysisRunId,
        byte[] expectedContentHash,
        byte[] currentDependencyFingerprint,
        CancellationToken cancellationToken = default);

    Task<PolicyCompilePanelResult> CompileNodeFilterArtifactsAsync(
        Guid nodeId,
        Guid analysisRunId,
        byte[] currentDependencyFingerprint,
        byte[] currentCapabilityHash,
        CancellationToken cancellationToken = default);
}

/// <summary>Default policy panel service (authoring + review orchestration).</summary>
public sealed class PolicyPanelService : IPolicyPanelService
{
    private const string DesktopAnalyzerVersion = "mfc.desktop.m2-18";
    private const string DesktopPolicySchemaVersion = "1";
    private const string DesktopPipelineVersion = "1";

    private readonly IPolicyServiceClient _client;

    public PolicyPanelService(IPolicyServiceClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<PolicyRevisionPanelState> LoadRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
    {
        PolicyRevision revision = await _client
            .GetPolicyRevisionAsync(revisionId, cancellationToken)
            .ConfigureAwait(false);
        return ToPanelState(revision);
    }

    public async Task<PolicyRevisionPanelState> CreateDraftAsync(
        string name,
        PolicyKind kind = PolicyKind.CompanyBaseline,
        CancellationToken cancellationToken = default)
    {
        PolicyDraft draft = await _client
            .CreateDraftPolicyAsync(name, kind, PolicyOwnerScope.Company, ownerId: null, cancellationToken)
            .ConfigureAwait(false);
        return await LoadRevisionAsync(DesktopProtoUuid.ToGuid(draft.RevisionId), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevisionPanelState> ValidateAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default)
    {
        PolicyRevision revision = await _client
            .ValidateRevisionAsync(revisionId, expectedContentHash, cancellationToken)
            .ConfigureAwait(false);
        return ToPanelState(revision);
    }

    public async Task<PolicyRevisionPanelState> SubmitForReviewAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default)
    {
        PolicyRevision revision = await _client
            .SubmitRevisionForReviewAsync(revisionId, expectedContentHash, cancellationToken)
            .ConfigureAwait(false);
        return ToPanelState(revision);
    }

    public async Task<PolicyRevisionPanelState> AddRuleAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        PolicyRuleEffect effectKind,
        string description,
        TrafficPredicate? predicate,
        CancellationToken cancellationToken = default)
    {
        _ = await _client.AddRuleAsync(
                revisionId,
                expectedContentHash,
                family,
                chain,
                stage,
                ordinal: 0,
                enabled: true,
                predicate,
                new RuleEffect { Kind = effectKind },
                description,
                cancellationToken)
            .ConfigureAwait(false);
        return await LoadRevisionAsync(revisionId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PolicyRevisionPanelState> UpdateRuleAsync(
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
        _ = await _client.UpdateRuleAsync(
                revisionId,
                ruleId,
                expectedContentHash,
                family,
                chain,
                stage,
                ordinal,
                enabled,
                predicate,
                new RuleEffect { Kind = effectKind },
                description,
                cancellationToken)
            .ConfigureAwait(false);
        return await LoadRevisionAsync(revisionId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PolicyRevisionPanelState> DeleteRuleAsync(
        Guid revisionId,
        Guid ruleId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default)
    {
        _ = await _client.DeleteRuleAsync(revisionId, ruleId, expectedContentHash, cancellationToken)
            .ConfigureAwait(false);
        return await LoadRevisionAsync(revisionId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PolicyRevisionPanelState> ReorderRulesInStageAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        IReadOnlyList<Guid> orderedRuleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderedRuleIds);
        if (orderedRuleIds.Count == 0)
        {
            throw new ArgumentException("Ordered rule ids must not be empty.", nameof(orderedRuleIds));
        }

        PolicyRevisionPanelState current = await LoadRevisionAsync(revisionId, cancellationToken)
            .ConfigureAwait(false);
        HashSet<Guid> stageMembers = current.Rules
            .Where(r => r.Family == family && r.Chain == chain && r.Stage == stage)
            .Select(r => r.Id)
            .ToHashSet();
        if (orderedRuleIds.Any(id => !stageMembers.Contains(id)))
        {
            throw new InvalidOperationException(
                "ReorderRules only allows rule ids within the same family/chain/stage group.");
        }

        if (orderedRuleIds.Count != stageMembers.Count
            || orderedRuleIds.Distinct().Count() != orderedRuleIds.Count)
        {
            throw new InvalidOperationException(
                "ReorderRules requires a contiguous permutation of all rules in the stage group.");
        }

        _ = await _client.ReorderRulesAsync(
                revisionId,
                expectedContentHash,
                family,
                chain,
                stage,
                orderedRuleIds,
                cancellationToken)
            .ConfigureAwait(false);
        return await LoadRevisionAsync(revisionId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PolicyRevisionPanelState> UpsertAddressObjectAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        string name,
        IpAddressFamily family,
        string entriesText,
        Guid? objectId = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AddressObjectEntry> entries = ParseAddressEntries(entriesText);
        PolicyRevision revision = await _client.UpsertAddressObjectAsync(
                revisionId,
                expectedContentHash,
                objectId,
                name,
                family,
                entries,
                description: null,
                cancellationToken)
            .ConfigureAwait(false);
        return ToPanelState(revision);
    }

    public async Task<PolicyRevisionPanelState> UpsertTcpServiceObjectAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        string name,
        uint tcpPort,
        Guid? objectId = null,
        CancellationToken cancellationToken = default)
    {
        if (tcpPort is 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(tcpPort), "TCP port must be 1..65535.");
        }

        ServiceTerm term = new()
        {
            Protocol = new IpProtocolSpec
            {
                Number = 6,
                CanonicalName = "tcp",
            },
        };
        term.DestinationPorts.Add(new PortInterval { Start = tcpPort, End = tcpPort });
        PolicyRevision revision = await _client.UpsertServiceObjectAsync(
                revisionId,
                expectedContentHash,
                objectId,
                name,
                [term],
                description: null,
                cancellationToken)
            .ConfigureAwait(false);
        return ToPanelState(revision);
    }

    public async Task<PolicyRevisionPanelState> ReplaceChainContractsAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        string disposition,
        RejectMode? rejectMode = null,
        CancellationToken cancellationToken = default)
    {
        string normalized = disposition.Trim();
        ChainContract contract = new()
        {
            Family = family,
            Chain = chain,
            DefaultDisposition = normalized,
        };
        if (string.Equals(normalized, "REJECT", StringComparison.OrdinalIgnoreCase))
        {
            if (rejectMode is null || rejectMode == RejectMode.Unspecified)
            {
                throw new ArgumentException("REJECT chain disposition requires an explicit reject_mode.");
            }

            contract.RejectMode = rejectMode.Value;
        }

        PolicyRevision current = await _client
            .GetPolicyRevisionAsync(revisionId, cancellationToken)
            .ConfigureAwait(false);
        List<ChainContract> next = [];
        bool replaced = false;
        foreach (ChainContract existing in current.ChainContracts)
        {
            if (existing.Family == family && existing.Chain == chain)
            {
                next.Add(contract);
                replaced = true;
            }
            else
            {
                next.Add(existing);
            }
        }

        if (!replaced)
        {
            next.Add(contract);
        }

        PolicyRevision revision = await _client.ReplaceChainContractsAsync(
                revisionId,
                expectedContentHash,
                next,
                cancellationToken)
            .ConfigureAwait(false);
        return ToPanelState(revision);
    }

    public async Task<PolicyRevisionPanelState> ReplaceTestsAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        string testsJson,
        CancellationToken cancellationToken = default)
    {
        PolicyRevision revision = await _client.ReplacePolicyTestsAsync(
                revisionId,
                expectedContentHash,
                testsJson,
                cancellationToken)
            .ConfigureAwait(false);
        return ToPanelState(revision);
    }

    public async Task<PolicyDiffPanelResult> DiffAsync(
        Guid beforeRevisionId,
        Guid afterRevisionId,
        CancellationToken cancellationToken = default)
    {
        PolicyRevisionDiff diff = await _client
            .DiffPolicyRevisionsAsync(beforeRevisionId, afterRevisionId, cancellationToken)
            .ConfigureAwait(false);
        List<PolicyFindingListItem> lines = [];
        foreach (string semantic in diff.SemanticClasses)
        {
            lines.Add(new PolicyFindingListItem { SummaryLine = $"semantic: {semantic}" });
        }

        foreach (string packet in diff.PacketSpaceClasses)
        {
            lines.Add(new PolicyFindingListItem { SummaryLine = $"packet-space: {packet}" });
        }

        foreach (string driver in diff.RiskDrivers)
        {
            lines.Add(new PolicyFindingListItem { SummaryLine = $"risk-driver: {driver}" });
        }

        foreach (PolicyRuleDiffLine rule in diff.RuleChanges)
        {
            Guid ruleId = DesktopProtoUuid.ToGuid(rule.RuleId);
            string changes = string.Join(", ", rule.Changes);
            lines.Add(new PolicyFindingListItem { SummaryLine = $"rule {ruleId:D}: {changes}" });
        }

        foreach (PolicyAnalysisFinding finding in diff.FindingSummaries)
        {
            lines.Add(new PolicyFindingListItem
            {
                SummaryLine = FormatFinding(finding.Code, finding.Severity, finding.Message, finding.Target),
            });
        }

        return new PolicyDiffPanelResult
        {
            RiskLevel = diff.RiskLevel,
            Lines = lines,
        };
    }

    public async Task<PolicyComposePanelResult> ComposeAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        EffectivePolicy effective = await _client
            .ComposeEffectivePolicyAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);
        byte[] hash = ToHashBytes(effective.LogicalEffectiveHash);
        return new PolicyComposePanelResult
        {
            LogicalEffectiveHash = hash,
            LogicalEffectiveHashHex = FormatHash(hash),
            Findings = effective.Findings
                .Select(f => new PolicyFindingListItem
                {
                    SummaryLine = FormatFinding(f.Code, severity: null, f.Message, f.Subject),
                })
                .ToArray(),
        };
    }

    public async Task<PolicyAnalysisRunListItem> RecordAnalysisRunAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        byte[] logicalEffectiveHash,
        string riskLevel,
        IReadOnlyList<PolicyFindingListItem>? composeFindings = null,
        CancellationToken cancellationToken = default)
    {
        // Residual: full NODE_EFFECTIVE / per-device analysis hashes need device context.
        // Desktop reuses the logical-effective (or content) hash slots so RecordAnalysisRun
        // remains callable for risk display + approve/bind wiring without RouterOS.
        byte[] contextHash = logicalEffectiveHash.Length == 32 ? logicalEffectiveHash : expectedContentHash;
        List<PolicyAnalysisFinding> findings = [];
        List<PolicyFindingListItem> ackable = [];
        if (composeFindings is not null)
        {
            foreach (PolicyFindingListItem item in composeFindings)
            {
                string code = string.IsNullOrWhiteSpace(item.Code) ? "DESKTOP_COMPOSE_FINDING" : item.Code.Trim();
                string target = string.IsNullOrWhiteSpace(item.Target) ? "compose" : item.Target.Trim();
                string message = string.IsNullOrWhiteSpace(item.Message) ? item.SummaryLine : item.Message.Trim();
                findings.Add(new PolicyAnalysisFinding
                {
                    Code = code,
                    Severity = "INFO",
                    Message = message,
                    Target = target,
                });
                byte[] warningHash = HashWarning(code, target, message);
                ackable.Add(new PolicyFindingListItem
                {
                    SummaryLine = FormatFinding(code, "INFO", message, target),
                    Code = code,
                    Target = target,
                    Message = message,
                    WarningHash = warningHash,
                });
            }
        }

        PolicyAnalysisRun run = await _client.RecordAnalysisRunAsync(
                revisionId,
                expectedContentHash,
                contextHash,
                contextHash,
                contextHash,
                contextHash,
                contextHash,
                [contextHash],
                contextHash,
                riskLevel,
                evidenceSignalsPresent: false,
                DesktopAnalyzerVersion,
                DesktopPolicySchemaVersion,
                DesktopPipelineVersion,
                findings,
                testResults: null,
                cancellationToken)
            .ConfigureAwait(false);
        return new PolicyAnalysisRunListItem
        {
            Id = DesktopProtoUuid.ToGuid(run.Id),
            RiskLevel = run.RiskLevel,
            EffectiveRiskLevel = run.EffectiveRiskLevel,
            BundleHash = ToHashBytes(run.BundleHash),
            DependencyFingerprint = ToHashBytes(run.DependencyFingerprint),
            AckableFindings = ackable,
        };
    }

    public async Task<PolicyAnalysisRunListItem> AcknowledgeWarningAsync(
        Guid analysisRunId,
        byte[] warningHash,
        CancellationToken cancellationToken = default)
    {
        PolicyAnalysisRun run = await _client
            .AcknowledgeWarningAsync(analysisRunId, warningHash, cancellationToken)
            .ConfigureAwait(false);
        return new PolicyAnalysisRunListItem
        {
            Id = DesktopProtoUuid.ToGuid(run.Id),
            RiskLevel = run.RiskLevel,
            EffectiveRiskLevel = run.EffectiveRiskLevel,
            BundleHash = ToHashBytes(run.BundleHash),
            DependencyFingerprint = ToHashBytes(run.DependencyFingerprint),
        };
    }

    public Task ApproveAsync(
        Guid revisionId,
        Guid analysisRunId,
        byte[] expectedContentHash,
        byte[] expectedBundleHash,
        byte[] currentDependencyFingerprint,
        CancellationToken cancellationToken = default)
        => _client.ApproveRevisionAsync(
            revisionId,
            analysisRunId,
            expectedContentHash,
            expectedBundleHash,
            currentDependencyFingerprint,
            cancellationToken);

    public Task BindAsync(
        Guid revisionId,
        Guid analysisRunId,
        byte[] expectedContentHash,
        byte[] currentDependencyFingerprint,
        CancellationToken cancellationToken = default)
        => _client.ActivateDesiredBindingAsync(
            revisionId,
            analysisRunId,
            expectedContentHash,
            currentDependencyFingerprint,
            cancellationToken);

    public async Task<PolicyCompilePanelResult> CompileNodeFilterArtifactsAsync(
        Guid nodeId,
        Guid analysisRunId,
        byte[] currentDependencyFingerprint,
        byte[] currentCapabilityHash,
        CancellationToken cancellationToken = default)
    {
        CompileNodeFilterArtifactsResponse response = await _client
            .CompileNodeFilterArtifactsAsync(
                nodeId,
                analysisRunId,
                currentDependencyFingerprint,
                currentCapabilityHash,
                cancellationToken)
            .ConfigureAwait(false);
        return new PolicyCompilePanelResult
        {
            NodeId = DesktopProtoUuid.ToGuid(response.NodeId),
            LogicalEffectiveHashHex = FormatHash(ToHashBytes(response.LogicalEffectivePolicyHash)),
            ArtifactLines = response.Artifacts.Select(a =>
                $"device={DesktopProtoUuid.ToGuid(a.DeviceId):D} artifact={a.ArtifactId} " +
                $"rules={a.RuleCount} new={a.StoredAsNew}").ToArray(),
        };
    }

    /// <summary>
    /// SHA-256 warning identity matching Domain <c>PolicyApprovalHasher.HashWarning</c>
    /// (<c>mfc.policy.warning.v1</c>) so Desktop can ack without referencing Domain.
    /// </summary>
    public static byte[] HashWarning(string code, string target, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(Encoding.UTF8.GetBytes("mfc.policy.warning.v1"));
        hasher.AppendData([(byte)0]);
        hasher.AppendData(Encoding.UTF8.GetBytes(code.Trim()));
        hasher.AppendData([(byte)0]);
        hasher.AppendData(Encoding.UTF8.GetBytes(target.Trim()));
        hasher.AppendData([(byte)0]);
        hasher.AppendData(Encoding.UTF8.GetBytes(message.Trim()));
        return hasher.GetHashAndReset();
    }

    /// <summary>Parses one CIDR/host/range entry per line into proto AddressObjectEntry values.</summary>
    public static IReadOnlyList<AddressObjectEntry> ParseAddressEntries(string entriesText)
    {
        if (string.IsNullOrWhiteSpace(entriesText))
        {
            throw new ArgumentException("Enter at least one address entry (host, CIDR, or range).", nameof(entriesText));
        }

        string[] lines = entriesText.Split(
            ['\n', '\r'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<AddressObjectEntry> entries = new(lines.Length);
        foreach (string line in lines)
        {
            if (line.Contains('/', StringComparison.Ordinal))
            {
                string[] parts = line.Split('/', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2
                    || !uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint prefix)
                    || prefix > 128)
                {
                    throw new ArgumentException($"Invalid CIDR entry '{line}'.");
                }

                entries.Add(new AddressObjectEntry
                {
                    Kind = "PREFIX",
                    Address = parts[0],
                    PrefixLength = prefix,
                });
                continue;
            }

            if (line.Contains('-', StringComparison.Ordinal))
            {
                string[] parts = line.Split('-', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2
                    || !IPAddress.TryParse(parts[0], out _)
                    || !IPAddress.TryParse(parts[1], out _))
                {
                    throw new ArgumentException($"Invalid range entry '{line}'.");
                }

                entries.Add(new AddressObjectEntry
                {
                    Kind = "RANGE",
                    Start = parts[0],
                    End = parts[1],
                });
                continue;
            }

            if (!IPAddress.TryParse(line, out _))
            {
                throw new ArgumentException($"Invalid host entry '{line}'.");
            }

            entries.Add(new AddressObjectEntry
            {
                Kind = "HOST",
                Address = line,
            });
        }

        return entries;
    }

    private static PolicyRevisionPanelState ToPanelState(PolicyRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        byte[] hash = ToHashBytes(revision.ContentHash);
        PolicyRevisionState state = revision.State;
        bool readOnly = state is not (PolicyRevisionState.Draft or PolicyRevisionState.Validated);
        return new PolicyRevisionPanelState
        {
            RevisionId = DesktopProtoUuid.ToGuid(revision.Id),
            PolicyId = DesktopProtoUuid.ToGuid(revision.PolicyId),
            State = state,
            StateText = state.ToString(),
            Kind = revision.Kind,
            KindText = revision.Kind.ToString(),
            ContentHash = hash,
            ContentHashHex = FormatHash(hash),
            IsReadOnly = readOnly,
            TestsJson = revision.TestsJson ?? string.Empty,
            Rules = revision.Rules.Select(ToRuleItem).ToArray(),
            AddressObjects = revision.AddressObjects.Select(ToAddressItem).ToArray(),
            ServiceObjects = revision.ServiceObjects.Select(ToServiceItem).ToArray(),
            ChainContracts = revision.ChainContracts.Select(ToContractItem).ToArray(),
            RevisionWarnings = revision.Warnings
                .Select(w => new PolicyFindingListItem
                {
                    SummaryLine = string.IsNullOrWhiteSpace(w.Subject)
                        ? $"{w.Code}: {w.Message}"
                        : $"{w.Code}({w.Subject}): {w.Message}",
                })
                .ToArray(),
        };
    }

    private static PolicyRuleListItem ToRuleItem(PolicyRule rule) => new()
    {
        Id = DesktopProtoUuid.ToGuid(rule.Id),
        Family = rule.Family,
        Chain = rule.Chain,
        Stage = rule.Stage,
        FamilyText = rule.Family.ToString(),
        ChainText = rule.Chain.ToString(),
        StageText = rule.Stage.ToString(),
        Ordinal = rule.Ordinal,
        Enabled = rule.Enabled,
        Effect = rule.Effect?.Kind ?? PolicyRuleEffect.Unspecified,
        EffectText = rule.Effect?.Kind.ToString() ?? "Unspecified",
        Description = rule.Description,
        WarningLines = rule.Warnings
            .Select(w => string.IsNullOrWhiteSpace(w.Subject) ? $"{w.Code}: {w.Message}" : $"{w.Code}({w.Subject}): {w.Message}")
            .ToArray(),
    };

    private static PolicyAddressObjectListItem ToAddressItem(AddressObject obj)
    {
        StringBuilder entries = new();
        foreach (AddressObjectEntry entry in obj.Entries)
        {
            if (entries.Length > 0)
            {
                entries.Append("; ");
            }

            entries.Append(entry.Kind switch
            {
                "PREFIX" => $"{entry.Address}/{entry.PrefixLength}",
                "RANGE" => $"{entry.Start}-{entry.End}",
                _ => entry.HasAddress ? entry.Address : entry.Kind,
            });
        }

        return new PolicyAddressObjectListItem
        {
            Id = DesktopProtoUuid.ToGuid(obj.Id),
            Name = obj.Name,
            FamilyText = obj.Family.ToString(),
            EntriesText = entries.ToString(),
        };
    }

    private static PolicyServiceObjectListItem ToServiceItem(ServiceObject obj)
    {
        string terms = string.Join(
            "; ",
            obj.Terms.Select(t =>
            {
                string proto = t.Protocol.HasCanonicalName
                    ? t.Protocol.CanonicalName
                    : t.Protocol.HasNumber
                        ? t.Protocol.Number.ToString(CultureInfo.InvariantCulture)
                        : t.Protocol.Any
                            ? "any"
                            : "?";
                string ports = t.DestinationPorts.Count == 0
                    ? string.Empty
                    : " dport=" + string.Join(',', t.DestinationPorts.Select(p =>
                        p.Start == p.End
                            ? p.Start.ToString(CultureInfo.InvariantCulture)
                            : $"{p.Start}-{p.End}"));
                return proto + ports;
            }));
        return new PolicyServiceObjectListItem
        {
            Id = DesktopProtoUuid.ToGuid(obj.Id),
            Name = obj.Name,
            TermsText = terms,
        };
    }

    private static PolicyChainContractListItem ToContractItem(ChainContract contract) => new()
    {
        FamilyText = contract.Family.ToString(),
        ChainText = contract.Chain.ToString(),
        Disposition = contract.DefaultDisposition,
        RejectModeText = contract.RejectMode == RejectMode.Unspecified
            ? null
            : contract.RejectMode.ToString(),
    };

    private static string FormatFinding(string code, string? severity, string message, string? subject)
    {
        string head = string.IsNullOrWhiteSpace(severity) ? code : $"{severity}/{code}";
        return string.IsNullOrWhiteSpace(subject) ? $"{head}: {message}" : $"{head}({subject}): {message}";
    }

    private static byte[] ToHashBytes(Sha256? hash)
    {
        if (hash is null || hash.Value.Length != 32)
        {
            return new byte[32];
        }

        return hash.Value.ToByteArray();
    }

    private static string FormatHash(byte[] hash)
        => Convert.ToHexString(hash).ToLowerInvariant();
}
