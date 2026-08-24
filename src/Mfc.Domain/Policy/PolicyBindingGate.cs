using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Desired-binding gate (Policy Model §10 / §22). Activation and expiry never deploy.
/// </summary>
public static class PolicyBindingGate
{
    /// <summary>Whether an APPROVED revision may become the desired binding.</summary>
    public static PolicyBindingEvaluation EvaluateActivation(
        PolicyRevision revision,
        Policy policy,
        PolicyAnalysisRun run,
        Hash256 currentDependencyFingerprint,
        IReadOnlyList<PolicyDesiredBinding> existingActive)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(currentDependencyFingerprint);
        ArgumentNullException.ThrowIfNull(existingActive);

        if (revision.State == PolicyRevisionState.Revoked)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingRevoked,
                "REVOKED revisions cannot become a desired binding.");
        }

        if (revision.State != PolicyRevisionState.Approved)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingNotApproved,
                "Desired binding requires an APPROVED revision.");
        }

        if (revision.ApprovedAnalysisRunId is null || revision.ApprovedBundleHash is null)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingNotApproved,
                "Desired binding requires the analysis run that completed approval.");
        }

        if (run.RevisionId != revision.Id || !run.RevisionContentHash.Equals(revision.ContentHash))
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BundleMismatch,
                "Binding analysis run does not match the approved revision.");
        }

        if (run.Id != revision.ApprovedAnalysisRunId.Value
            || !run.BundleHash.Equals(revision.ApprovedBundleHash))
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BundleMismatch,
                "Binding must use the analysis run that completed approval.");
        }

        PolicyApprovalFinding? blocker = run.Findings.FirstOrDefault(
            static f => f.Severity == PolicyEvidenceAnalysisCodes.SeverityBlocker);
        if (blocker is not null)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.Blocker,
                $"Blocker {blocker.Code} forbids binding.");
        }

        if (!run.DependencyFingerprint.Equals(currentDependencyFingerprint))
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingStale,
                "Binding context is stale: dependency fingerprint changed.");
        }

        PolicyBindingScope scope = PolicyDesiredBinding.ScopeFor(policy.Kind);
        Guid? scopeId = scope == PolicyBindingScope.Company ? null : policy.OwnerId;

        if (scope == PolicyBindingScope.Exception)
        {
            int activeExceptions = existingActive.Count(b =>
                b.State == PolicyBindingState.Active
                && b.Scope == PolicyBindingScope.Exception
                && NullableEquals(b.ScopeId, scopeId)
                && b.PolicyId != policy.Id);
            if (activeExceptions >= PolicyDesiredBinding.MaxActiveExceptionsPerScope)
            {
                return PolicyBindingEvaluation.Reject(
                    PolicyApprovalCodes.BindingCardinality,
                    "Active EXCEPTION bindings exceed the 256 cap.");
            }

            return PolicyBindingEvaluation.Ok();
        }

        // COMPANY/SITE/NODE: at most one ACTIVE; a different revision replaces the previous.
        return PolicyBindingEvaluation.Ok();
    }

    /// <summary>Whether an EXCEPTION binding may move to EXPIRED_PENDING_RECONCILIATION.</summary>
    public static PolicyBindingEvaluation EvaluateExpiry(
        PolicyDesiredBinding binding,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.Scope != PolicyBindingScope.Exception)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingNotException,
                "Only EXCEPTION bindings may expire.");
        }

        if (binding.State != PolicyBindingState.Active)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingNotDue,
                "Only ACTIVE exception bindings may expire.");
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        if (binding.ValidUntilUtc is null || now < binding.ValidUntilUtc.Value)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingNotDue,
                "Exception binding is not past valid_until.");
        }

        return PolicyBindingEvaluation.Ok();
    }

    /// <summary>Whether an INCIDENT_DENY_OVERLAY binding may move to EXPIRED_PENDING_RECONCILIATION.</summary>
    public static PolicyBindingEvaluation EvaluateIncidentOverlayExpiry(
        PolicyDesiredBinding binding,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.Scope != PolicyBindingScope.IncidentDenyOverlay)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingNotIncidentOverlay,
                "Only INCIDENT_DENY_OVERLAY bindings may expire.");
        }

        if (binding.State != PolicyBindingState.Active)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingNotDue,
                "Only ACTIVE incident deny overlay bindings may expire.");
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        if (binding.ValidUntilUtc is null || now < binding.ValidUntilUtc.Value)
        {
            return PolicyBindingEvaluation.Reject(
                PolicyApprovalCodes.BindingNotDue,
                "Incident deny overlay binding is not past valid_until.");
        }

        return PolicyBindingEvaluation.Ok();
    }

    private static bool NullableEquals(Guid? left, Guid? right)
        => left == right;
}
