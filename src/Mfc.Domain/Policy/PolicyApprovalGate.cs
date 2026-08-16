using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Pure approval gate (Policy Model §63–§65). Does not mutate revision, bind, or deploy.
/// </summary>
public static class PolicyApprovalGate
{
    /// <summary>Evaluates whether a reviewer vote may be recorded and whether it completes APPROVED.</summary>
    public static PolicyApprovalEvaluation Evaluate(
        PolicyRevision revision,
        Policy policy,
        PolicyAnalysisRun run,
        Hash256 expectedBundleHash,
        Hash256 currentDependencyFingerprint,
        IReadOnlyList<PolicyWarningAcknowledgment> acknowledgments,
        IReadOnlyList<PolicyApproval> existingVotes,
        UserId reviewerId,
        bool isSecurityOwner)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(expectedBundleHash);
        ArgumentNullException.ThrowIfNull(currentDependencyFingerprint);
        ArgumentNullException.ThrowIfNull(acknowledgments);
        ArgumentNullException.ThrowIfNull(existingVotes);

        if (revision.State != PolicyRevisionState.InReview)
        {
            return PolicyApprovalEvaluation.Reject(
                PolicyApprovalCodes.NotInReview,
                $"Approval requires IN_REVIEW; actual {revision.State}.");
        }

        if (run.RevisionId != revision.Id)
        {
            return PolicyApprovalEvaluation.Reject(
                PolicyApprovalCodes.MissingRun,
                "Analysis run does not belong to this revision.");
        }

        if (!run.RevisionContentHash.Equals(revision.ContentHash))
        {
            return PolicyApprovalEvaluation.Reject(
                PolicyApprovalCodes.BundleMismatch,
                "Analysis run content_hash does not match the current revision.");
        }

        if (!run.BundleHash.Equals(expectedBundleHash))
        {
            return PolicyApprovalEvaluation.Reject(
                PolicyApprovalCodes.BundleMismatch,
                "Approval is bound to the exact analysis bundle hash.");
        }

        if (!run.DependencyFingerprint.Equals(currentDependencyFingerprint))
        {
            return PolicyApprovalEvaluation.Reject(
                PolicyApprovalCodes.Stale,
                "Approval context is stale: dependency fingerprint changed.");
        }

        PolicyApprovalFinding? blocker = run.Findings.FirstOrDefault(
            static f => f.Severity == PolicyEvidenceAnalysisCodes.SeverityBlocker);
        if (blocker is not null)
        {
            return PolicyApprovalEvaluation.Reject(
                PolicyApprovalCodes.Blocker,
                $"Blocker {blocker.Code} forbids approval.");
        }

        foreach (PolicyApprovalTestOutcome test in run.TestResults)
        {
            bool system = test.Origin == PolicyEvidenceAnalysisCodes.OriginSystem;
            if (test.Outcome != PolicyEvidenceAnalysisCodes.OutcomePass
                || (system && test.Proof != PolicyEvidenceAnalysisCodes.ProofProven)
                || test.Proof == PolicyEvidenceAnalysisCodes.ProofIndeterminate)
            {
                return PolicyApprovalEvaluation.Reject(
                    PolicyApprovalCodes.TestsFailed,
                    $"Mandatory test {test.TestId} is not a proven PASS.");
            }
        }

        HashSet<string> acked = new(
            acknowledgments
                .Where(a => a.AnalysisRunId == run.Id)
                .Select(a => a.WarningHash.ToString()),
            StringComparer.Ordinal);
        foreach (PolicyApprovalFinding warning in run.Findings.Where(
                     static f => f.Severity == PolicyEvidenceAnalysisCodes.SeverityWarning))
        {
            if (!acked.Contains(warning.WarningHash.ToString()))
            {
                return PolicyApprovalEvaluation.Reject(
                    PolicyApprovalCodes.WarningUnacked,
                    $"Warning {warning.Code} requires acknowledgment of hash {warning.WarningHash}.");
            }
        }

        if (policy.Kind == PolicyKind.Exception)
        {
            ExceptionMetadata? metadata = PolicyDocumentReader.Read(revision.CanonicalBytes).ExceptionMetadata;
            if (metadata is null
                || string.IsNullOrWhiteSpace(metadata.Reason)
                || string.IsNullOrWhiteSpace(metadata.TicketReference)
                || metadata.ValidUntil == default)
            {
                return PolicyApprovalEvaluation.Reject(
                    PolicyApprovalCodes.Blocker,
                    "EXCEPTION approval requires reason, ticket, and expiry.");
            }
        }

        string risk = run.EffectiveRiskLevel();
        IReadOnlyList<PolicyApproval> matchingVotes = existingVotes
            .Where(v => v.RevisionId == revision.Id && v.BundleHash.Equals(run.BundleHash))
            .ToArray();
        if (matchingVotes.Any(v => v.ReviewerId == reviewerId))
        {
            return PolicyApprovalEvaluation.Reject(
                PolicyApprovalCodes.SeparationOfDuties,
                "This reviewer already recorded an approval vote for this bundle.");
        }

        bool authorIsReviewer = reviewerId == revision.CreatedBy;
        if (risk is PolicyEvidenceAnalysisCodes.RiskHigh or PolicyEvidenceAnalysisCodes.RiskCritical
            && authorIsReviewer)
        {
            return PolicyApprovalEvaluation.Reject(
                PolicyApprovalCodes.SeparationOfDuties,
                "HIGH/CRITICAL changes require a reviewer distinct from the author.");
        }

        if (risk is PolicyEvidenceAnalysisCodes.RiskLow or PolicyEvidenceAnalysisCodes.RiskMedium
            or PolicyEvidenceAnalysisCodes.RiskNone or PolicyEvidenceAnalysisCodes.RiskHigh)
        {
            return PolicyApprovalEvaluation.Approve();
        }

        int distinctReviewers = matchingVotes
            .Select(static v => v.ReviewerId.Value)
            .Append(reviewerId.Value)
            .Distinct()
            .Count();
        bool hasSecurity = isSecurityOwner || matchingVotes.Any(static v => v.IsSecurityOwner);
        if (distinctReviewers >= 2 && hasSecurity)
        {
            return PolicyApprovalEvaluation.Approve();
        }

        return PolicyApprovalEvaluation.RecordVote();
    }
}
