namespace Mfc.Domain.Policy;

/// <summary>Frozen compiler error codes (Compiler Spec §28). No partial artifact on failure.</summary>
public static class PolicyCompilerCodes
{
    public const string AddressSelectorEmpty = "ADDRESS_SELECTOR_EMPTY";

    public const string AddressListLimitExceeded = "ADDRESS_LIST_LIMIT_EXCEEDED";

    public const string AddressEntryLimitExceeded = "ADDRESS_ENTRY_LIMIT_EXCEEDED";

    public const string ResourceNameCollision = "RESOURCE_NAME_COLLISION";

    public const string ZoneNotResolved = "ZONE_NOT_RESOLVED";

    public const string ZoneEmpty = "ZONE_EMPTY";

    public const string ZoneInterfaceMissing = "ZONE_INTERFACE_MISSING";

    public const string ZoneDynamicInterface = "ZONE_DYNAMIC_INTERFACE";

    public const string ZoneExpansionLimit = "ZONE_EXPANSION_LIMIT";

    public const string ServiceTermTooLarge = "SERVICE_TERM_TOO_LARGE";

    public const string RuleVariantLimit = "RULE_VARIANT_LIMIT";

    public const string CompilerAnalysisStale = "COMPILER_ANALYSIS_STALE";

    public const string UnsupportedMatcher = "UNSUPPORTED_MATCHER";

    public const string RejectModeUnsupported = "REJECT_MODE_UNSUPPORTED";

    public const string FasttrackContextUnsupported = "FASTTRACK_CONTEXT_UNSUPPORTED";

    public const string FasttrackLoggingUnsupported = "FASTTRACK_LOGGING_UNSUPPORTED";

    public const string FasttrackCapabilityUnsupported = "FASTTRACK_CAPABILITY_UNSUPPORTED";

    public const string FilterRuleLimit = "FILTER_RULE_LIMIT";

    public const string CompilerInputNotApproved = "COMPILER_INPUT_NOT_APPROVED";

    public const string CompilerCapabilityStale = "COMPILER_CAPABILITY_STALE";

    public const string CompilerProfileUnsupported = "COMPILER_PROFILE_UNSUPPORTED";

    public const string ArtifactSizeLimit = "ARTIFACT_SIZE_LIMIT";

    /// <summary>Compile blockers that must map to FailedPrecondition when orchestration exists.</summary>
    public static bool IsFailedPrecondition(string code)
        => code is AddressSelectorEmpty
            or AddressListLimitExceeded
            or AddressEntryLimitExceeded
            or ResourceNameCollision
            or ZoneNotResolved
            or ZoneEmpty
            or ZoneInterfaceMissing
            or ZoneDynamicInterface
            or ZoneExpansionLimit
            or ServiceTermTooLarge
            or RuleVariantLimit
            or CompilerAnalysisStale
            or UnsupportedMatcher
            or RejectModeUnsupported
            or FasttrackContextUnsupported
            or FasttrackLoggingUnsupported
            or FasttrackCapabilityUnsupported
            or FilterRuleLimit
            or CompilerInputNotApproved
            or CompilerCapabilityStale
            or CompilerProfileUnsupported
            or ArtifactSizeLimit;
}
