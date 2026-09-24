using Mfc.Application.Common;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>
/// AUDIT-AN-03 / F06: Controller-authoritative analysis facts for <c>RecordAnalysisRun</c>.
/// Client may supply soft WARNING findings and CAS hashes; risk, evidence flag, and test
/// outcomes are never taken from the client as authority.
/// </summary>
internal static class PolicyServerOwnedAnalysis
{
    /// <summary>Opaque document tests the server cannot execute yet stay fail-closed.</summary>
    public const string OpaqueTestIndeterminateCode = "POLICY_ANALYSIS_OPAQUE_TEST_INDETERMINATE";

    public sealed class Facts
    {
        public required IReadOnlyList<PolicyApprovalFinding> Findings { get; init; }

        public required IReadOnlyList<PolicyApprovalTestOutcome> TestResults { get; init; }

        public required string RiskLevel { get; init; }

        public required bool EvidenceSignalsPresent { get; init; }
    }

    /// <summary>
    /// Builds server-owned findings, tests, risk, and evidence flag.
    /// Client PASS/PROVEN outcomes are discarded; mandatory opaque tests become FAIL/INDETERMINATE.
    /// </summary>
    public static Facts Build(
        Policy policy,
        PolicyRevision revision,
        PolicyDocument document,
        IReadOnlyList<PolicyApprovalFindingInput> clientFindings)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(clientFindings);

        List<PolicyApprovalFinding> findings = [];
        foreach (PolicyApprovalFinding structural in PolicyRevisionStructuralAnalysis
                     .CollectStructuralApprovalFindings(policy, revision, document))
        {
            findings.Add(structural);
        }

        foreach (PolicyApprovalFindingInput input in clientFindings)
        {
            // Soft intent only: never accept client BLOCKER (server structural owns blockers).
            string severity = PolicyEvidenceAnalysisCodes.SeverityWarning;
            string code = string.IsNullOrWhiteSpace(input.Code) ? "CLIENT_FINDING" : input.Code.Trim();
            string target = input.Target?.Trim() ?? string.Empty;
            string message = string.IsNullOrWhiteSpace(input.Message) ? "Client-supplied finding." : input.Message.Trim();
            Hash256 warningHash = PolicyApprovalHasher.HashWarning(code, target, message);
            if (findings.Any(f => f.WarningHash.Equals(warningHash)))
            {
                continue;
            }

            findings.Add(new PolicyApprovalFinding
            {
                Code = code,
                Severity = severity,
                Message = message,
                Target = target,
                WarningHash = warningHash,
            });
        }

        HashSet<Guid> required = PolicyCatalogViewMapper.ExtractTestIds(document.Tests);
        List<PolicyApprovalTestOutcome> tests = [];
        foreach (Guid testId in required.OrderBy(static g => g))
        {
            tests.Add(new PolicyApprovalTestOutcome
            {
                TestId = new PolicyTestId(testId),
                Origin = PolicyEvidenceAnalysisCodes.OriginUser,
                Outcome = PolicyEvidenceAnalysisCodes.OutcomeFail,
                Proof = PolicyEvidenceAnalysisCodes.ProofIndeterminate,
            });
            string target = testId.ToString("D");
            string message =
                "Opaque document test was not executed by Controller; client PASS is not authoritative.";
            Hash256 warningHash = PolicyApprovalHasher.HashWarning(
                OpaqueTestIndeterminateCode, target, message);
            if (findings.Any(f => f.WarningHash.Equals(warningHash)))
            {
                continue;
            }

            findings.Add(new PolicyApprovalFinding
            {
                Code = OpaqueTestIndeterminateCode,
                Severity = PolicyEvidenceAnalysisCodes.SeverityWarning,
                Message = message,
                Target = target,
                WarningHash = warningHash,
            });
        }

        string risk = DeriveRisk(findings);
        // Opaque document tests are not executed here; evidence is incomplete until typed evaluation.
        bool evidencePresent = required.Count == 0;
        return new Facts
        {
            Findings = findings,
            TestResults = tests,
            RiskLevel = risk,
            EvidenceSignalsPresent = evidencePresent,
        };
    }

    public static string DeriveRisk(IReadOnlyList<PolicyApprovalFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        if (findings.Any(static f => f.Severity == PolicyEvidenceAnalysisCodes.SeverityBlocker))
        {
            return PolicyEvidenceAnalysisCodes.RiskCritical;
        }

        if (findings.Any(static f => f.Severity == PolicyEvidenceAnalysisCodes.SeverityWarning))
        {
            return PolicyEvidenceAnalysisCodes.RiskHigh;
        }

        return PolicyEvidenceAnalysisCodes.RiskLow;
    }
}
