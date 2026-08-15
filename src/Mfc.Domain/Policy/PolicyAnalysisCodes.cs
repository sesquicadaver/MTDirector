namespace Mfc.Domain.Policy;

/// <summary>
/// Frozen structural / satisfiability / sequence finding codes (Policy Model §38–§42).
/// </summary>
public static class PolicyAnalysisCodes
{
    public const string SeverityBlocker = "BLOCKER";

    /// <summary>Resolved packet space is empty (Policy Model §39).</summary>
    public const string Unsatisfiable = "RULE_UNSATISFIABLE";

    /// <summary>INPUT+egress or OUTPUT+ingress zone selectors (Policy Model §22).</summary>
    public const string ZoneDirection = "RULE_ZONE_DIRECTION";

    /// <summary>TCP flags or TCP_RESET used without a TCP-capable service union.</summary>
    public const string TcpFlagsProtocol = "RULE_TCP_FLAGS_PROTOCOL";

    /// <summary>ICMP terms that do not match the rule address family.</summary>
    public const string IcmpFamily = "RULE_ICMP_FAMILY";

    /// <summary>IPsec IN on OUTPUT or IPsec OUT on INPUT.</summary>
    public const string IpsecDirection = "RULE_IPSEC_DIRECTION";

    /// <summary>Mutually exclusive connection-state combination.</summary>
    public const string ConnectionState = "RULE_CONNECTION_STATE";

    /// <summary>Matcher outside the TrafficPredicate v1 surface (Policy Model §25).</summary>
    public const string UnsupportedMatcher = "RULE_UNSUPPORTED_MATCHER";

    public const string SeverityWarning = "WARNING";

    /// <summary>Equal normalized predicate, effect, and logging (Policy Model §40.1).</summary>
    public const string ExactDuplicate = "RULE_EXACT_DUPLICATE";

    /// <summary>Equal predicate, different effect; later rule is unreachable (Policy Model §40.2).</summary>
    public const string ConflictingDuplicate = "RULE_CONFLICTING_DUPLICATE";

    /// <summary>Enabled rule residual is empty after previous terminals (Policy Model §41.1).</summary>
    public const string FullyShadowed = "RULE_FULLY_SHADOWED";

    /// <summary>Previous terminals removed some but not all of the rule space.</summary>
    public const string PartiallyShadowed = "RULE_PARTIALLY_SHADOWED";

    /// <summary>Shadow residual exceeded fragment limits or subtract emptied without fail-closed cover.</summary>
    public const string ShadowIndeterminate = "SHADOW_ANALYSIS_INDETERMINATE";

    /// <summary>Earlier ACCEPT overlaps a later DROP/REJECT (Policy Model §42).</summary>
    public const string EarlierAllowBypassesDeny = "EARLIER_ALLOW_BYPASSES_DENY";

    /// <summary>Order-dependent deny/allow or REJECT/DROP overlap.</summary>
    public const string OrderDependentOverlap = "ORDER_DEPENDENT_OVERLAP";

    /// <summary>Overlapping same-class effects that are not exact duplicates.</summary>
    public const string RedundantOverlap = "REDUNDANT_OVERLAP";

    /// <summary>FASTTRACK_ACCEPT overlaps another rule on the same surface.</summary>
    public const string FasttrackOverlap = "FASTTRACK_OVERLAP";

    /// <summary>
    /// Sequence BLOCKER codes that are not <c>RULE_*</c> prefixes and must still
    /// map to FailedPrecondition on compose (Policy Model §41.1 / §42).
    /// </summary>
    public static bool IsSequenceComposeFailure(string code)
        => code is ShadowIndeterminate or EarlierAllowBypassesDeny or FasttrackOverlap;
}
