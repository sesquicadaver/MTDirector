namespace Mfc.Domain.Policy;

/// <summary>Frozen actual-filter CFG / pre-anchor codes (Policy Model §44–§45 / M2-12).</summary>
public static class ActualFilterAnalysisCodes
{
    public const string SeverityBlocker = "BLOCKER";

    public const string SeverityWarning = "WARNING";

    /// <summary>Jump call-stack contains the target chain (Policy Model §45.1).</summary>
    public const string JumpCycle = "ACTUAL_FILTER_JUMP_CYCLE";

    /// <summary>Jump nesting exceeded 16 (Policy Model §45.1).</summary>
    public const string DepthLimit = "ACTUAL_FILTER_DEPTH_LIMIT";

    /// <summary>Unsupported stateful or unknown action.</summary>
    public const string UnknownAction = "ACTUAL_FILTER_UNKNOWN_ACTION";

    /// <summary>Matcher outside the allowlisted actual-filter surface.</summary>
    public const string UnknownMatcher = "ACTUAL_FILTER_UNKNOWN_MATCHER";

    /// <summary>CFG limits, missing jump target, or split-cover safety hole.</summary>
    public const string AnalysisIndeterminate = "ACTUAL_FILTER_ANALYSIS_INDETERMINATE";

    /// <summary>Unmanaged pre-anchor ACCEPT can skip the managed pipeline.</summary>
    public const string PreAnchorAcceptBypasses = "PRE_ANCHOR_ACCEPT_BYPASSES_POLICY";

    /// <summary>Unmanaged pre-anchor DROP/REJECT shadows the managed pipeline.</summary>
    public const string PreAnchorDropShadows = "PRE_ANCHOR_DROP_SHADOWS_POLICY";

    /// <summary>Unmanaged pre-anchor FastTrack can skip the managed pipeline.</summary>
    public const string PreAnchorFasttrackBypasses = "PRE_ANCHOR_FASTTRACK_BYPASSES_POLICY";

    /// <summary>Dynamic rule sits before the managed anchor.</summary>
    public const string PreAnchorDynamicRule = "PRE_ANCHOR_DYNAMIC_RULE_PRESENT";

    /// <summary>Pre-anchor effect cannot be proven (unknown matcher/action/limits).</summary>
    public const string PreAnchorIndeterminate = "PRE_ANCHOR_ANALYSIS_INDETERMINATE";

    public const int MaxChains = 1024;

    public const int MaxJumpDepth = 16;

    public const int MaxGraphNodes = 50_000;

    /// <summary>Actual-filter / pre-anchor codes that must map to FailedPrecondition.</summary>
    public static bool IsFailedPrecondition(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        return code.StartsWith("ACTUAL_FILTER_", StringComparison.Ordinal)
               || code.StartsWith("PRE_ANCHOR_", StringComparison.Ordinal);
    }
}
