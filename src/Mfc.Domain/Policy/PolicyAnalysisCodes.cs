namespace Mfc.Domain.Policy;

/// <summary>
/// Frozen structural / satisfiability finding codes (Policy Model §38–§39 / M2-10).
/// Sequence findings (<c>RULE_EXACT_DUPLICATE</c>, shadow codes) are M2-11 and must not be emitted here.
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
}
