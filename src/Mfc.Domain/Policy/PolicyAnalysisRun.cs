using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Immutable completed analysis run (Policy Model §66–§67). Insert-only after create.
/// </summary>
public sealed class PolicyAnalysisRun
{
    public PolicyAnalysisRunId Id { get; }

    public PolicyRevisionId RevisionId { get; }

    public Hash256 RevisionContentHash { get; }

    public Hash256 LogicalEffectiveHash { get; }

    public Hash256 AnalysisContextHash { get; }

    public Hash256 EvidenceContextHash { get; }

    public Hash256 TopologyProjectionHash { get; }

    public Hash256 ImpactSetHash { get; }

    public IReadOnlyList<Hash256> PerDeviceAnalysisHashes { get; }

    public Hash256 BundleHash { get; }

    public Hash256 DependencyFingerprint { get; }

    public string RiskLevel { get; }

    public bool EvidenceSignalsPresent { get; }

    public string AnalyzerVersion { get; }

    public string PolicySchemaVersion { get; }

    public string PipelineVersion { get; }

    public IReadOnlyList<PolicyApprovalFinding> Findings { get; }

    public IReadOnlyList<PolicyApprovalTestOutcome> TestResults { get; }

    public UserId CreatedBy { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    private PolicyAnalysisRun(
        PolicyAnalysisRunId id,
        PolicyRevisionId revisionId,
        Hash256 revisionContentHash,
        Hash256 logicalEffectiveHash,
        Hash256 analysisContextHash,
        Hash256 evidenceContextHash,
        Hash256 topologyProjectionHash,
        Hash256 impactSetHash,
        IReadOnlyList<Hash256> perDeviceAnalysisHashes,
        Hash256 bundleHash,
        Hash256 dependencyFingerprint,
        string riskLevel,
        bool evidenceSignalsPresent,
        string analyzerVersion,
        string policySchemaVersion,
        string pipelineVersion,
        IReadOnlyList<PolicyApprovalFinding> findings,
        IReadOnlyList<PolicyApprovalTestOutcome> testResults,
        UserId createdBy,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        RevisionId = revisionId;
        RevisionContentHash = revisionContentHash;
        LogicalEffectiveHash = logicalEffectiveHash;
        AnalysisContextHash = analysisContextHash;
        EvidenceContextHash = evidenceContextHash;
        TopologyProjectionHash = topologyProjectionHash;
        ImpactSetHash = impactSetHash;
        PerDeviceAnalysisHashes = perDeviceAnalysisHashes;
        BundleHash = bundleHash;
        DependencyFingerprint = dependencyFingerprint;
        RiskLevel = riskLevel;
        EvidenceSignalsPresent = evidenceSignalsPresent;
        AnalyzerVersion = analyzerVersion;
        PolicySchemaVersion = policySchemaVersion;
        PipelineVersion = pipelineVersion;
        Findings = findings;
        TestResults = testResults;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Records a completed run and freezes the computed bundle hash.</summary>
    public static PolicyAnalysisRun Create(
        PolicyRevisionId revisionId,
        Hash256 revisionContentHash,
        Hash256 logicalEffectiveHash,
        Hash256 analysisContextHash,
        Hash256 evidenceContextHash,
        Hash256 topologyProjectionHash,
        Hash256 impactSetHash,
        IReadOnlyList<Hash256> perDeviceAnalysisHashes,
        Hash256 dependencyFingerprint,
        string riskLevel,
        bool evidenceSignalsPresent,
        string analyzerVersion,
        string policySchemaVersion,
        string pipelineVersion,
        IReadOnlyList<PolicyApprovalFinding> findings,
        IReadOnlyList<PolicyApprovalTestOutcome> testResults,
        UserId createdBy,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(revisionContentHash);
        ArgumentNullException.ThrowIfNull(logicalEffectiveHash);
        ArgumentNullException.ThrowIfNull(analysisContextHash);
        ArgumentNullException.ThrowIfNull(evidenceContextHash);
        ArgumentNullException.ThrowIfNull(topologyProjectionHash);
        ArgumentNullException.ThrowIfNull(impactSetHash);
        ArgumentNullException.ThrowIfNull(perDeviceAnalysisHashes);
        ArgumentNullException.ThrowIfNull(dependencyFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(riskLevel);
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(policySchemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineVersion);
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(testResults);
        if (riskLevel is not (
            PolicyEvidenceAnalysisCodes.RiskNone
            or PolicyEvidenceAnalysisCodes.RiskLow
            or PolicyEvidenceAnalysisCodes.RiskMedium
            or PolicyEvidenceAnalysisCodes.RiskHigh
            or PolicyEvidenceAnalysisCodes.RiskCritical))
        {
            throw new DomainInvariantException($"Unknown risk level '{riskLevel}'.");
        }

        List<PolicyApprovalFinding> frozenFindings = [];
        foreach (PolicyApprovalFinding finding in findings)
        {
            ArgumentNullException.ThrowIfNull(finding);
            if (finding.Severity is not (
                PolicyEvidenceAnalysisCodes.SeverityBlocker or PolicyEvidenceAnalysisCodes.SeverityWarning))
            {
                throw new DomainInvariantException($"Unknown finding severity '{finding.Severity}'.");
            }

            Hash256 expected = PolicyApprovalHasher.HashWarning(finding.Code, finding.Target, finding.Message);
            if (!expected.Equals(finding.WarningHash))
            {
                throw new DomainInvariantException("Finding warning_hash does not match code/target/message.");
            }

            frozenFindings.Add(finding);
        }

        List<PolicyApprovalTestOutcome> frozenTests = [];
        foreach (PolicyApprovalTestOutcome test in testResults)
        {
            ArgumentNullException.ThrowIfNull(test);
            if (test.Origin is not (
                PolicyEvidenceAnalysisCodes.OriginSystem or PolicyEvidenceAnalysisCodes.OriginUser))
            {
                throw new DomainInvariantException($"Unknown test origin '{test.Origin}'.");
            }

            if (test.Outcome is not (
                PolicyEvidenceAnalysisCodes.OutcomePass or PolicyEvidenceAnalysisCodes.OutcomeFail))
            {
                throw new DomainInvariantException($"Unknown test outcome '{test.Outcome}'.");
            }

            if (test.Proof is not (
                PolicyEvidenceAnalysisCodes.ProofProven or PolicyEvidenceAnalysisCodes.ProofIndeterminate))
            {
                throw new DomainInvariantException($"Unknown test proof '{test.Proof}'.");
            }

            frozenTests.Add(test);
        }

        Hash256 bundle = PolicyApprovalHasher.HashAnalysisBundle(
            logicalEffectiveHash,
            perDeviceAnalysisHashes,
            topologyProjectionHash,
            impactSetHash);
        return new PolicyAnalysisRun(
            PolicyAnalysisRunId.New(),
            revisionId,
            revisionContentHash,
            logicalEffectiveHash,
            analysisContextHash,
            evidenceContextHash,
            topologyProjectionHash,
            impactSetHash,
            perDeviceAnalysisHashes.ToArray(),
            bundle,
            dependencyFingerprint,
            riskLevel,
            evidenceSignalsPresent,
            analyzerVersion.Trim(),
            policySchemaVersion.Trim(),
            pipelineVersion.Trim(),
            frozenFindings,
            frozenTests,
            createdBy,
            createdAtUtc.ToUniversalTime());
    }

    /// <summary>Rebuilds a completed run from persistence. Payload fields stay frozen.</summary>
    public static PolicyAnalysisRun Reconstitute(
        PolicyAnalysisRunId id,
        PolicyRevisionId revisionId,
        Hash256 revisionContentHash,
        Hash256 logicalEffectiveHash,
        Hash256 analysisContextHash,
        Hash256 evidenceContextHash,
        Hash256 topologyProjectionHash,
        Hash256 impactSetHash,
        IReadOnlyList<Hash256> perDeviceAnalysisHashes,
        Hash256 bundleHash,
        Hash256 dependencyFingerprint,
        string riskLevel,
        bool evidenceSignalsPresent,
        string analyzerVersion,
        string policySchemaVersion,
        string pipelineVersion,
        IReadOnlyList<PolicyApprovalFinding> findings,
        IReadOnlyList<PolicyApprovalTestOutcome> testResults,
        UserId createdBy,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(perDeviceAnalysisHashes);
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(testResults);
        Hash256 actual = PolicyApprovalHasher.HashAnalysisBundle(
            logicalEffectiveHash,
            perDeviceAnalysisHashes,
            topologyProjectionHash,
            impactSetHash);
        if (!actual.Equals(bundleHash))
        {
            throw new DomainInvariantException("Stored analysis bundle hash does not match run components.");
        }

        return new PolicyAnalysisRun(
            id,
            revisionId,
            revisionContentHash,
            logicalEffectiveHash,
            analysisContextHash,
            evidenceContextHash,
            topologyProjectionHash,
            impactSetHash,
            perDeviceAnalysisHashes.ToArray(),
            bundleHash,
            dependencyFingerprint,
            riskLevel,
            evidenceSignalsPresent,
            analyzerVersion,
            policySchemaVersion,
            pipelineVersion,
            findings.ToArray(),
            testResults.ToArray(),
            createdBy,
            createdAtUtc.ToUniversalTime());
    }

    /// <summary>
    /// Effective SoD risk. Missing <see cref="PolicyEvidenceSignals"/> is unknown CRITICAL,
    /// never silent NONE (M2-16 residual / M2-17).
    /// </summary>
    public string EffectiveRiskLevel()
        => EvidenceSignalsPresent ? RiskLevel : PolicyEvidenceAnalysisCodes.RiskCritical;

    /// <summary>
    /// True when the run has no BLOCKER findings and every test is a proven PASS
    /// (same gate as approval / compiler <c>AnalysisPassed</c>).
    /// </summary>
    public bool IsPass()
    {
        if (Findings.Any(static f => f.Severity == PolicyEvidenceAnalysisCodes.SeverityBlocker))
        {
            return false;
        }

        foreach (PolicyApprovalTestOutcome test in TestResults)
        {
            bool system = test.Origin == PolicyEvidenceAnalysisCodes.OriginSystem;
            if (test.Outcome != PolicyEvidenceAnalysisCodes.OutcomePass
                || (system && test.Proof != PolicyEvidenceAnalysisCodes.ProofProven)
                || test.Proof == PolicyEvidenceAnalysisCodes.ProofIndeterminate)
            {
                return false;
            }
        }

        return true;
    }
}
