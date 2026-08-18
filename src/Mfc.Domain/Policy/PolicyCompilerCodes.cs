namespace Mfc.Domain.Policy;

/// <summary>Frozen compiler error codes (Compiler Spec §28). No partial artifact on failure.</summary>
public static class PolicyCompilerCodes
{
    public const string AddressSelectorEmpty = "ADDRESS_SELECTOR_EMPTY";

    public const string AddressListLimitExceeded = "ADDRESS_LIST_LIMIT_EXCEEDED";

    public const string AddressEntryLimitExceeded = "ADDRESS_ENTRY_LIMIT_EXCEEDED";

    public const string ResourceNameCollision = "RESOURCE_NAME_COLLISION";

    /// <summary>Compile blockers that must map to FailedPrecondition when orchestration exists.</summary>
    public static bool IsFailedPrecondition(string code)
        => code is AddressSelectorEmpty
            or AddressListLimitExceeded
            or AddressEntryLimitExceeded
            or ResourceNameCollision;
}
