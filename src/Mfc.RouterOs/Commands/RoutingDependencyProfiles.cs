namespace Mfc.RouterOs.Commands;

/// <summary>Property profiles for routing and firewall-dependency reads (Spec §26–32, M1-14).</summary>
internal static class RoutingDependencyProfiles
{
    public static RosPropertyProfile RoutingTables { get; } = new(
        "routing_tables",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("fib"),
            P("disabled"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
            P("used", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile RoutingRules { get; } = new(
        "routing_rules",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("action"),
            P("src-address"),
            P("dst-address"),
            P("interface"),
            P("routing-mark"),
            P("table"),
            P("min-prefix"),
            P("disabled"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("inactive", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile Ipv4StaticRoutes { get; } = StaticRoutes("ipv4_static_routes");

    public static RosPropertyProfile Ipv6StaticRoutes { get; } = StaticRoutes("ipv6_static_routes");

    public static RosPropertyProfile Ipv4DefaultRouteState { get; } = DefaultRouteState("ipv4_default_route_state");

    public static RosPropertyProfile Ipv6DefaultRouteState { get; } = DefaultRouteState("ipv6_default_route_state");

    public static RosPropertyProfile Ipv4Settings { get; } = new(
        "ipv4_settings",
        [
            P("ip-forward"),
            P("send-redirects"),
            P("accept-source-route"),
            P("accept-redirects"),
            P("secure-redirects"),
            P("rp-filter"),
            P("tcp-syncookies"),
            P("max-neighbor-entries"),
            P("arp-timeout"),
            P("allow-fast-path"),
            P("route-cache"),
            P("ipv4-fast-path-active", RosPropertyClassification.ObservationTyped),
            P("ipv4-fasttrack-active", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile Ipv6Settings { get; } = new(
        "ipv6_settings",
        [
            P("disable-ipv6"),
            P("forward"),
            P("accept-redirects"),
            P("accept-router-advertisements"),
            P("max-neighbor-entries"),
            P("multipath-hash-policy"),
        ]);

    public static RosPropertyProfile Ipv4Nat { get; } = NatRawMangle(
        "ipv4_nat",
        includeNat: true,
        includeMangle: false,
        ipv6: false);

    public static RosPropertyProfile Ipv6Nat { get; } = NatRawMangle(
        "ipv6_nat",
        includeNat: true,
        includeMangle: false,
        ipv6: true);

    public static RosPropertyProfile Ipv4Raw { get; } = NatRawMangle(
        "ipv4_raw",
        includeNat: false,
        includeMangle: false,
        ipv6: false);

    public static RosPropertyProfile Ipv6Raw { get; } = NatRawMangle(
        "ipv6_raw",
        includeNat: false,
        includeMangle: false,
        ipv6: true);

    public static RosPropertyProfile Ipv4Mangle { get; } = NatRawMangle(
        "ipv4_mangle",
        includeNat: false,
        includeMangle: true,
        ipv6: false);

    public static RosPropertyProfile Ipv6Mangle { get; } = NatRawMangle(
        "ipv6_mangle",
        includeNat: false,
        includeMangle: true,
        ipv6: true);

    private static RosPropertyProfile StaticRoutes(string id)
        => new(
            id,
            [
                P(".id", RosPropertyClassification.RawOnly),
                P("dst-address"),
                P("gateway"),
                P("routing-table"),
                P("pref-src"),
                P("distance"),
                P("scope"),
                P("target-scope"),
                P("check-gateway"),
                P("type"),
                P("blackhole"),
                P("unreachable"),
                P("prohibit"),
                P("disabled"),
                P("comment", redaction: RosRedactionPolicy.LogRedacted),
                P("static", RosPropertyClassification.ObservationTyped),
                P("dynamic", RosPropertyClassification.ObservationTyped),
                P("active", RosPropertyClassification.ObservationTyped),
                P("inactive", RosPropertyClassification.ObservationTyped),
                P("ecmp", RosPropertyClassification.ObservationTyped),
                P("immediate-gw", RosPropertyClassification.ObservationTyped),
                P("gateway-status", RosPropertyClassification.ObservationTyped),
                P("local-address", RosPropertyClassification.ObservationTyped),
            ]);

    private static RosPropertyProfile DefaultRouteState(string id)
        => new(
            id,
            [
                P(".id", RosPropertyClassification.RawOnly),
                P("dst-address", RosPropertyClassification.ObservationTyped),
                P("routing-table", RosPropertyClassification.ObservationTyped),
                P("gateway", RosPropertyClassification.ObservationTyped),
                P("distance", RosPropertyClassification.ObservationTyped),
                P("active", RosPropertyClassification.ObservationTyped),
                P("inactive", RosPropertyClassification.ObservationTyped),
                P("dynamic", RosPropertyClassification.ObservationTyped),
                P("static", RosPropertyClassification.ObservationTyped),
                P("immediate-gw", RosPropertyClassification.ObservationTyped),
                P("gateway-status", RosPropertyClassification.ObservationTyped),
                P("pref-src", RosPropertyClassification.ObservationTyped),
                P("disabled", RosPropertyClassification.ObservationTyped),
            ]);

    private static RosPropertyProfile NatRawMangle(string id, bool includeNat, bool includeMangle, bool ipv6)
    {
        List<RosPropertyDefinition> properties =
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("chain"),
            P("action"),
            P("disabled"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("protocol"),
            P("src-address"),
            P("dst-address"),
            P("src-address-list"),
            P("dst-address-list"),
            P("src-port"),
            P("dst-port"),
            P("in-interface"),
            P("out-interface"),
            P("in-interface-list"),
            P("out-interface-list"),
            P("connection-state"),
            P("connection-mark"),
            P("packet-mark"),
            P("routing-mark"),
            P("jump-target"),
            P("address-list"),
            P("address-list-timeout"),
            P("per-connection-classifier", RosPropertyClassification.ConfigOpaque),
            P("nth", RosPropertyClassification.ConfigOpaque),
            P("random", RosPropertyClassification.ConfigOpaque),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
        ];

        if (includeNat)
        {
            if (ipv6)
            {
                properties.Add(P("to-address"));
            }
            else
            {
                properties.Add(P("to-addresses"));
                properties.Add(P("to-ports"));
            }
        }

        if (includeMangle)
        {
            properties.AddRange(
            [
                P("new-connection-mark"),
                P("new-packet-mark"),
                P("new-routing-mark"),
                P("passthrough"),
            ]);
            if (ipv6)
            {
                properties.Add(P("new-hop-limit"));
            }
            else
            {
                properties.Add(P("new-ttl"));
            }
        }

        return new RosPropertyProfile(id, properties);
    }

    private static RosPropertyDefinition P(
        string name,
        RosPropertyClassification classification = RosPropertyClassification.ConfigTyped,
        RosRedactionPolicy redaction = RosRedactionPolicy.None)
    {
        if (string.Equals(name, "comment", StringComparison.Ordinal)
            || string.Equals(name, "note", StringComparison.Ordinal))
        {
            redaction = RosRedactionPolicy.LogRedacted;
        }

        return new RosPropertyDefinition(name, classification, redaction);
    }
}
