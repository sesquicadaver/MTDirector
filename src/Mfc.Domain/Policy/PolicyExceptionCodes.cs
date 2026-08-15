namespace Mfc.Domain.Policy;

/// <summary>Frozen <c>POLICY_EXCEPTION_*</c> compose blockers (M2-08 LOCK-6).</summary>
public static class PolicyExceptionCodes
{
    public const string MetadataInvalid = "POLICY_EXCEPTION_METADATA_INVALID";

    public const string TargetNotFound = "POLICY_EXCEPTION_TARGET_NOT_FOUND";

    public const string TargetNotEligible = "POLICY_EXCEPTION_TARGET_NOT_ELIGIBLE";

    public const string StageMismatch = "POLICY_EXCEPTION_STAGE_MISMATCH";

    public const string FamilyChainMismatch = "POLICY_EXCEPTION_FAMILY_CHAIN_MISMATCH";

    public const string NotSubset = "POLICY_EXCEPTION_NOT_SUBSET";

    public const string UniverseTarget = "POLICY_EXCEPTION_UNIVERSE_TARGET";

    public const string Overlap = "POLICY_EXCEPTION_OVERLAP";

    public const string MandatoryDeny = "POLICY_EXCEPTION_MANDATORY_DENY";

    public const string Effect = "POLICY_EXCEPTION_EFFECT";

    public const string RuleCount = "POLICY_EXCEPTION_RULE_COUNT";

    public const string ParentContextMismatch = "POLICY_EXCEPTION_PARENT_CONTEXT_MISMATCH";

    public const string StageOwnership = "POLICY_EXCEPTION_STAGE_OWNERSHIP";

    public const string ObjectsForbidden = "POLICY_EXCEPTION_OBJECTS_FORBIDDEN";
}
