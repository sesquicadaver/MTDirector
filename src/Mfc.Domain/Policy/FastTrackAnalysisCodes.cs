namespace Mfc.Domain.Policy;

/// <summary>Frozen FastTrack safety codes (Policy Model §52 / Compiler §21 / M2-15).</summary>
public static class FastTrackAnalysisCodes
{
    public const string SeverityBlocker = "BLOCKER";

    public const string SeverityWarning = "WARNING";

    public const string RiskHigh = "HIGH";

    /// <summary>Family/chain/stage/protocol/connection-state outside the FastTrack allowlist.</summary>
    public const string ContextUnsupported = "FASTTRACK_CONTEXT_UNSUPPORTED";

    /// <summary>Logging is forbidden on FASTTRACK_ACCEPT (Compiler §21).</summary>
    public const string LoggingUnsupported = "FASTTRACK_LOGGING_UNSUPPORTED";

    /// <summary>Missing conntrack, HotSpot, queue-tree, or other capability hole.</summary>
    public const string CapabilityUnsupported = "FASTTRACK_CAPABILITY_UNSUPPORTED";

    /// <summary>Compiler must emit the adjacent ACCEPT fallback (not a compose blocker).</summary>
    public const string FallbackRequired = "FASTTRACK_FALLBACK_REQUIRED";

    /// <summary>
    /// FastTrack BLOCKERs that must map to FailedPrecondition.
    /// Fallback is a compiler contract, not a compose failure.
    /// </summary>
    public static bool IsFailedPrecondition(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        if (string.Equals(code, FallbackRequired, StringComparison.Ordinal))
        {
            return false;
        }

        return code.StartsWith("FASTTRACK_", StringComparison.Ordinal)
               || string.Equals(code, ActualFilterAnalysisCodes.PreAnchorFasttrackBypasses, StringComparison.Ordinal);
    }
}
