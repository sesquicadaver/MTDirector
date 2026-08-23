namespace Mfc.RouterOs.Commands;

/// <summary>Property profiles for on-demand connection-tracking reads (M7.3-03 / next-2 §2).</summary>
internal static class ConnectionTrackingAllowlistProfiles
{
    public static RosPropertyProfile Ipv4FirewallConnections { get; } = new(
        "ipv4_firewall_connections",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("protocol"),
            P("src-address"),
            P("dst-address"),
            P("reply-src-address", RosPropertyClassification.ObservationTyped),
            P("reply-dst-address", RosPropertyClassification.ObservationTyped),
            P("tcp-state", RosPropertyClassification.ObservationTyped),
            P("timeout", RosPropertyClassification.ObservationTyped),
            P("srcnat", RosPropertyClassification.ObservationTyped),
            P("dstnat", RosPropertyClassification.ObservationTyped),
            P("fasttrack", RosPropertyClassification.ObservationTyped),
            P("hw-offload", RosPropertyClassification.ObservationTyped),
            P("connection-mark", RosPropertyClassification.ObservationTyped),
            P("routing-mark", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile Ipv6FirewallConnections { get; } = new(
        "ipv6_firewall_connections",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("protocol"),
            P("src-address"),
            P("dst-address"),
            P("reply-src-address", RosPropertyClassification.ObservationTyped),
            P("reply-dst-address", RosPropertyClassification.ObservationTyped),
            P("tcp-state", RosPropertyClassification.ObservationTyped),
            P("timeout", RosPropertyClassification.ObservationTyped),
            P("srcnat", RosPropertyClassification.ObservationTyped),
            P("dstnat", RosPropertyClassification.ObservationTyped),
            P("fasttrack", RosPropertyClassification.ObservationTyped),
            P("hw-offload", RosPropertyClassification.ObservationTyped),
            P("connection-mark", RosPropertyClassification.ObservationTyped),
            P("routing-mark", RosPropertyClassification.ObservationTyped),
        ]);

    private static RosPropertyDefinition P(
        string name,
        RosPropertyClassification classification = RosPropertyClassification.ConfigTyped,
        RosRedactionPolicy redaction = RosRedactionPolicy.None)
        => new(name, classification, redaction);
}
