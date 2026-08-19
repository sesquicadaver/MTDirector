namespace Mfc.Domain.Policy;

/// <summary>Frozen management-path safety codes (Policy Model §46 / M2-13).</summary>
public static class ManagementPathAnalysisCodes
{
    public const string SeverityBlocker = "BLOCKER";

    /// <summary>No enabled input/output management guard on the physical device.</summary>
    public const string GuardMissing = "MANAGEMENT_GUARD_MISSING";

    /// <summary>Guard is at or after the managed anchor, or the candidate would rewrite it.</summary>
    public const string GuardMoved = "MANAGEMENT_GUARD_MOVED";

    /// <summary>Guard predicate is wider than GuardProfile (Onboarding §17 / M5-03).</summary>
    public const string GuardTooBroad = "MANAGEMENT_GUARD_TOO_BROAD";

    /// <summary>Guard marker, static/enabled state, or matcher set is invalid (Onboarding §58 / M5-03).</summary>
    public const string GuardInvalid = "MANAGEMENT_GUARD_INVALID";

    /// <summary>API-SSL is missing, disabled, or the live port does not match the profile.</summary>
    public const string ServiceDisabled = "MANAGEMENT_SERVICE_DISABLED";

    /// <summary>A controller source prefix is outside the API-SSL IP-service allowlist.</summary>
    public const string SourceNotAllowed = "MANAGEMENT_SOURCE_NOT_ALLOWED";

    /// <summary>TCP NEW to API-SSL is not proven accepted on INPUT.</summary>
    public const string InputBlocked = "MANAGEMENT_INPUT_BLOCKED";

    /// <summary>TCP ESTABLISHED reply from API-SSL is not proven accepted on OUTPUT.</summary>
    public const string OutputBlocked = "MANAGEMENT_OUTPUT_BLOCKED";

    /// <summary>Unknown matcher, VIP-only destination, DNS destination, or other unprovable result.</summary>
    public const string PathIndeterminate = "MANAGEMENT_PATH_INDETERMINATE";

    /// <summary>Management-path codes that must map to FailedPrecondition.</summary>
    public static bool IsFailedPrecondition(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        return code.StartsWith("MANAGEMENT_", StringComparison.Ordinal);
    }
}
