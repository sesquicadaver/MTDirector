namespace Mfc.RouterOs.Commands;

/// <summary>
/// Compile-time routing-assurance read allowlist surface (M7.1-01).
/// Covers Spec §3 mandatory sections; Controller must not manage routing writes.
/// M7.1-01 registers <see cref="RoutingAssuranceAllowlist"/> paths (settings, VRF, filter rules);
/// M7.1-02 maps those sections into ConfigurationHashMaterial / RoutingAssuranceState.
/// </summary>
public static class RoutingAssuranceAllowlist
{
    private static readonly RosReadCommandId[] CommandSet =
    [
        RosReadCommandId.RoutingTables,
        RosReadCommandId.RoutingSettings,
        RosReadCommandId.RoutingRules,
        RosReadCommandId.IpVrfs,
        RosReadCommandId.Ipv4StaticRoutes,
        RosReadCommandId.Ipv6StaticRoutes,
        RosReadCommandId.Ipv4DefaultRouteState,
        RosReadCommandId.Ipv6DefaultRouteState,
        RosReadCommandId.RoutingFilterRules,
        RosReadCommandId.RoutingFilterSelectRules,
    ];

    public static IReadOnlyList<RosReadCommandId> CommandIds => CommandSet;

    /// <summary>
    /// Distinct /print paths for Spec §3 (one entry per menu; multiple command ids may share a path).
    /// </summary>
    public static IReadOnlyList<string> FixedPaths { get; } =
    [
        "/routing/table/print",
        "/routing/settings/print",
        "/routing/rule/print",
        "/ip/vrf/print",
        "/ip/route/print",
        "/ipv6/route/print",
        "/routing/filter/rule/print",
        "/routing/filter/select-rule/print",
    ];

    /// <summary>Property names that must never appear on routing-assurance allowlist profiles.</summary>
    public static IReadOnlyList<string> ForbiddenPropertyNames { get; } =
    [
        "password",
        "secret",
        "passphrase",
        "private-key",
        "psk",
        "shared-key",
        "auth-key",
        "token",
        "api-key",
    ];
}
