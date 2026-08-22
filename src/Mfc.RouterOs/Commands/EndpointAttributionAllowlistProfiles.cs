namespace Mfc.RouterOs.Commands;

/// <summary>Property profiles for endpoint-attribution reads (M7.2-01 / next-2 §3).</summary>
internal static class EndpointAttributionAllowlistProfiles
{
    public static RosPropertyProfile Ipv4Arp { get; } = new(
        "ipv4_arp",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("address"),
            P("mac-address"),
            P("interface"),
            P("published", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
            P("complete", RosPropertyClassification.ObservationTyped),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("disabled"),
        ]);

    public static RosPropertyProfile Ipv6Neighbors { get; } = new(
        "ipv6_neighbors",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("address"),
            P("mac-address"),
            P("interface"),
            P("status", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("disabled"),
        ]);

    public static RosPropertyProfile DhcpServerLeases { get; } = new(
        "dhcp_server_leases",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("address"),
            P("mac-address"),
            P("active-mac-address", RosPropertyClassification.ObservationTyped),
            P("server"),
            P("status", RosPropertyClassification.ObservationTyped),
            P("last-seen", RosPropertyClassification.ObservationTyped),
            P("expires-after", RosPropertyClassification.ObservationTyped),
            P("host-name", redaction: RosRedactionPolicy.LogRedacted),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("disabled"),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("blocked", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile BridgeHosts { get; } = new(
        "bridge_hosts",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("mac-address"),
            P("interface"),
            P("bridge"),
            P("on-interface"),
            P("invalid", RosPropertyClassification.ObservationTyped),
            P("local", RosPropertyClassification.ObservationTyped),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("disabled"),
        ]);

    public static RosPropertyProfile DhcpSnoopingBindings { get; } = new(
        "dhcp_snooping_bindings",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("mac-address"),
            P("address"),
            P("server"),
            P("bridge"),
            P("interface"),
            P("vlan"),
            P("lease-time", RosPropertyClassification.ObservationTyped),
            P("age", RosPropertyClassification.ObservationTyped),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("disabled"),
        ]);

    public static RosPropertyProfile WireGuardPeers { get; } = new(
        "wireguard_peers",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("interface"),
            P("name"),
            P("allowed-address"),
            P("current-endpoint-address", RosPropertyClassification.ObservationTyped),
            P("last-handshake", RosPropertyClassification.ObservationTyped),
            P("rx", RosPropertyClassification.ObservationTyped),
            P("tx", RosPropertyClassification.ObservationTyped),
            P("disabled"),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
        ]);

    public static RosPropertyProfile IpsecActivePeers { get; } = new(
        "ipsec_active_peers",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("remote-address"),
            P("local-address"),
            P("side", RosPropertyClassification.ObservationTyped),
            P("state", RosPropertyClassification.ObservationTyped),
            P("uptime", RosPropertyClassification.ObservationTyped),
            P("ph2-total", RosPropertyClassification.ObservationTyped),
            P("dynamic", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile PppActiveSessions { get; } = new(
        "ppp_active_sessions",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("service"),
            P("address"),
            P("caller-id", RosPropertyClassification.ObservationTyped),
            P("encoding", RosPropertyClassification.ObservationTyped),
            P("session-id", RosPropertyClassification.ObservationTyped),
            P("uptime", RosPropertyClassification.ObservationTyped),
            P("radius", RosPropertyClassification.ObservationTyped),
            P("dynamic", RosPropertyClassification.ObservationTyped),
        ]);

    private static RosPropertyDefinition P(
        string name,
        RosPropertyClassification classification = RosPropertyClassification.ConfigTyped,
        RosRedactionPolicy redaction = RosRedactionPolicy.None)
    {
        if (string.Equals(name, "comment", StringComparison.Ordinal)
            || string.Equals(name, "note", StringComparison.Ordinal)
            || string.Equals(name, "host-name", StringComparison.Ordinal))
        {
            redaction = RosRedactionPolicy.LogRedacted;
        }

        return new RosPropertyDefinition(name, classification, redaction);
    }
}
