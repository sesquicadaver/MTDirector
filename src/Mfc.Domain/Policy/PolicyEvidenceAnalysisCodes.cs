namespace Mfc.Domain.Policy;

/// <summary>Frozen policy-test / revision-diff / risk codes (Policy Model §54–§61 / M2-16).</summary>
public static class PolicyEvidenceAnalysisCodes
{
    public const string SeverityBlocker = "BLOCKER";

    public const string SeverityWarning = "WARNING";

    public const string ProofProven = "PROVEN";

    public const string ProofIndeterminate = "INDETERMINATE";

    public const string OutcomePass = "PASS";

    public const string OutcomeFail = "FAIL";

    public const string OriginUser = "USER";

    public const string OriginSystem = "SYSTEM";

    public const string ModeManagedOnly = "MANAGED_ONLY";

    public const string ModeNodeEffective = "NODE_EFFECTIVE";

    /// <summary>A SYSTEM test was disabled or omitted from execution.</summary>
    public const string SystemTestDisabled = "POLICY_TEST_SYSTEM_DISABLED";

    /// <summary>A safety test failed or was INDETERMINATE (Policy Model §57).</summary>
    public const string SafetyTestFailed = "POLICY_TEST_SAFETY_FAILED";

    /// <summary>NODE_EFFECTIVE evaluation lacked a proven actual-filter path.</summary>
    public const string NodeEffectiveIndeterminate = "POLICY_TEST_NODE_EFFECTIVE_INDETERMINATE";

    public const string RiskNone = "NONE";

    public const string RiskLow = "LOW";

    public const string RiskMedium = "MEDIUM";

    public const string RiskHigh = "HIGH";

    public const string RiskCritical = "CRITICAL";

    public const string ClassNoEffectiveChange = "NO_EFFECTIVE_CHANGE";

    public const string ClassRestrictive = "RESTRICTIVE";

    public const string ClassPermissive = "PERMISSIVE";

    public const string ClassMixed = "MIXED";

    public const string ClassControlPlane = "CONTROL_PLANE";

    public const string ClassFastTrack = "FASTTRACK";

    public const string ClassException = "EXCEPTION";

    public const string ClassDefaultDisposition = "DEFAULT_DISPOSITION";

    public const string ClassZoneBinding = "ZONE_BINDING";

    public const string PacketNewlyAccepted = "NEWLY_ACCEPTED";

    public const string PacketNewlyDenied = "NEWLY_DENIED";

    public const string PacketRejectChanged = "CHANGED_REJECT_BEHAVIOR";

    public const string ChangeAdded = "ADDED";

    public const string ChangeRemoved = "REMOVED";

    public const string ChangeModified = "MODIFIED";

    public const string ChangeMoved = "MOVED";

    public const string ChangeEnabled = "ENABLED";

    public const string ChangeDisabled = "DISABLED";

    /// <summary>Safety-test BLOCKERs that must map to FailedPrecondition.</summary>
    public static bool IsFailedPrecondition(string code)
        => code is SystemTestDisabled or SafetyTestFailed or NodeEffectiveIndeterminate;
}
