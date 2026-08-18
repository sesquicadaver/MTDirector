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
            or CompilerAnalysisStale;
}
