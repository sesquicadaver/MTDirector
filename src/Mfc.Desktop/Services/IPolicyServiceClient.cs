using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Contracts-only PolicyService client (ADR 0005 / M2-18 authoring + review).</summary>
public interface IPolicyServiceClient
{
    Task<PolicyDraft> CreateDraftPolicyAsync(
        string name,
        PolicyKind kind,
        PolicyOwnerScope ownerScope,
        Guid? ownerId = null,
        CancellationToken cancellationToken = default);

    Task<PolicyRevision> GetPolicyRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default);

    Task<ListRulesResponse> ListRulesAsync(
        Guid revisionId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<PolicyRuleMutation> AddRuleAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        uint ordinal,
        bool enabled,
        TrafficPredicate? predicate,
        RuleEffect effect,
        string description,
        CancellationToken cancellationToken = default);

    Task<PolicyRuleMutation> ReorderRulesAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        IReadOnlyList<Guid> orderedRuleIds,
        CancellationToken cancellationToken = default);

    Task<PolicyRevision> ValidateRevisionAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default);

    Task<PolicyRevision> UpsertAddressObjectAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        Guid? objectId,
        string name,
        IpAddressFamily family,
        IReadOnlyList<AddressObjectEntry> entries,
        string? description = null,
        CancellationToken cancellationToken = default);

    Task<PolicyRevision> UpsertServiceObjectAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        Guid? objectId,
        string name,
        IReadOnlyList<ServiceTerm> terms,
        string? description = null,
        CancellationToken cancellationToken = default);

    Task<PolicyRevision> ReplaceChainContractsAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IReadOnlyList<ChainContract> contracts,
        CancellationToken cancellationToken = default);

    Task<PolicyRevision> ReplacePolicyTestsAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        string? testsJson,
        CancellationToken cancellationToken = default);

    Task<PolicyRevisionDiff> DiffPolicyRevisionsAsync(
        Guid beforeRevisionId,
        Guid afterRevisionId,
        CancellationToken cancellationToken = default);

    Task<EffectivePolicy> ComposeEffectivePolicyAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default);

    Task<PolicyRevision> SubmitRevisionForReviewAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default);

    Task<PolicyAnalysisRun> RecordAnalysisRunAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        byte[] logicalEffectiveHash,
        byte[] analysisContextHash,
        byte[] evidenceContextHash,
        byte[] topologyProjectionHash,
        byte[] impactSetHash,
        IReadOnlyList<byte[]> perDeviceAnalysisHashes,
        byte[] dependencyFingerprint,
        string riskLevel,
        bool evidenceSignalsPresent,
        string analyzerVersion,
        string policySchemaVersion,
        string pipelineVersion,
        IReadOnlyList<PolicyAnalysisFinding>? findings = null,
        IReadOnlyList<PolicyAnalysisTestResult>? testResults = null,
        CancellationToken cancellationToken = default);

    Task<PolicyApprovalVote> ApproveRevisionAsync(
        Guid revisionId,
        Guid analysisRunId,
        byte[] expectedContentHash,
        byte[] expectedBundleHash,
        byte[] currentDependencyFingerprint,
        CancellationToken cancellationToken = default);

    Task<PolicyBinding> ActivateDesiredBindingAsync(
        Guid revisionId,
        Guid analysisRunId,
        byte[] expectedContentHash,
        byte[] currentDependencyFingerprint,
        CancellationToken cancellationToken = default);
}
