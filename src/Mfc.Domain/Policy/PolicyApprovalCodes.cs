namespace Mfc.Domain.Policy;

/// <summary>Frozen approval and desired-binding codes (Policy Model §63–§67 / M2-17).</summary>
public static class PolicyApprovalCodes
{
    public const string AnalyzerVersion = "mfc.policy-approval.v1";

    public const string BundlePrefix = "mfc.policy.analysis_bundle.v1";

    public const string DependencyPrefix = "mfc.policy.approval_deps.v1";

    public const string WarningPrefix = "mfc.policy.warning.v1";

    public const string OutcomeApprove = "APPROVE";

    public const string OutcomeRecordVote = "RECORD_VOTE";

    public const string OutcomeReject = "REJECT";

    public const string Blocker = "POLICY_APPROVAL_BLOCKER";

    public const string BundleMismatch = "POLICY_APPROVAL_BUNDLE_MISMATCH";

    public const string Stale = "POLICY_APPROVAL_STALE";

    public const string WarningUnacked = "POLICY_APPROVAL_WARNING_UNACKED";

    public const string SeparationOfDuties = "POLICY_APPROVAL_SOD";

    public const string TestsFailed = "POLICY_APPROVAL_TESTS_FAILED";

    public const string NotInReview = "POLICY_APPROVAL_NOT_IN_REVIEW";

    public const string MissingRun = "POLICY_APPROVAL_MISSING_RUN";

    public const string BindingNotApproved = "POLICY_BINDING_NOT_APPROVED";

    public const string BindingRevoked = "POLICY_BINDING_REVOKED";

    public const string BindingCardinality = "POLICY_BINDING_CARDINALITY";

    public const string BindingStale = "POLICY_BINDING_STALE";

    public const string BindingNotException = "POLICY_BINDING_NOT_EXCEPTION";

    public const string BindingNotDue = "POLICY_BINDING_NOT_DUE";

    /// <summary>Approval/binding blockers that must map to FailedPrecondition.</summary>
    public static bool IsFailedPrecondition(string code)
        => code.StartsWith("POLICY_APPROVAL_", StringComparison.Ordinal)
           || code.StartsWith("POLICY_BINDING_", StringComparison.Ordinal);
}
