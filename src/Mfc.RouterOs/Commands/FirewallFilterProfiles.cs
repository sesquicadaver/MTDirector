namespace Mfc.RouterOs.Commands;

/// <summary>Property profiles for firewall filter and address-list reads (Read Adapter Spec §24–30).</summary>
internal static class FirewallFilterProfiles
{
    public static RosPropertyProfile Ipv4Filter { get; } = BuildFilterProfile(
        "ipv4_filter",
        includeIpv4Only: true,
        includeIpv6Only: false);

    public static RosPropertyProfile Ipv6Filter { get; } = BuildFilterProfile(
        "ipv6_filter",
        includeIpv4Only: false,
        includeIpv6Only: true);

    public static RosPropertyProfile Ipv4AddressList { get; } = BuildAddressListProfile("ipv4_address_lists");

    public static RosPropertyProfile Ipv6AddressList { get; } = BuildAddressListProfile("ipv6_address_lists");

    private static RosPropertyProfile BuildAddressListProfile(string id)
        => new(
            id,
            [
                P(".id", RosPropertyClassification.RawOnly),
                P("list"),
                P("address"),
                P("timeout", RosPropertyClassification.ObservationTyped),
                P("disabled"),
                P("comment", redaction: RosRedactionPolicy.LogRedacted),
                P("dynamic", RosPropertyClassification.ObservationTyped),
            ]);

    private static RosPropertyProfile BuildFilterProfile(string id, bool includeIpv4Only, bool includeIpv6Only)
    {
        List<RosPropertyDefinition> properties =
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("chain"),
            P("action"),
            P("disabled"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("log"),
            P("log-prefix"),
            P("protocol"),
            P("src-address"),
            P("dst-address"),
            P("src-address-list"),
            P("dst-address-list"),
            P("src-address-type"),
            P("dst-address-type"),
            P("src-port"),
            P("dst-port"),
            P("port"),
            P("in-interface"),
            P("out-interface"),
            P("in-interface-list"),
            P("out-interface-list"),
            P("in-bridge-port"),
            P("out-bridge-port"),
            P("in-bridge-port-list"),
            P("out-bridge-port-list"),
            P("src-mac-address"),
            P("connection-state"),
            P("connection-nat-state"),
            P("connection-mark"),
            P("connection-type"),
            P("connection-bytes", RosPropertyClassification.ConfigOpaque),
            P("connection-limit", RosPropertyClassification.ConfigOpaque),
            P("connection-rate", RosPropertyClassification.ConfigOpaque),
            P("packet-mark"),
            P("routing-mark"),
            P("tcp-flags"),
            P("tcp-mss"),
            P("icmp-options"),
            P("ipsec-policy"),
            P("helper"),
            P("tls-host"),
            P("layer7-protocol"),
            P("content"),
            P("dscp"),
            P("priority"),
            P("ingress-priority"),
            P("packet-size"),
            P("limit", RosPropertyClassification.ConfigOpaque),
            P("dst-limit", RosPropertyClassification.ConfigOpaque),
            P("time", RosPropertyClassification.ConfigOpaque),
            P("random"),
            P("nth", RosPropertyClassification.ConfigOpaque),
            P("per-connection-classifier", RosPropertyClassification.ConfigOpaque),
            // Action-specific (incl. FastTrack / address-list actions).
            P("jump-target"),
            P("reject-with"),
            P("address-list"),
            P("address-list-timeout"),
            P("hw-offload"),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
            // bytes/packets intentionally absent (TRANSIENT_EXCLUDED).
        ];

        if (includeIpv4Only)
        {
            properties.AddRange(
            [
                P("fragment"),
                P("ipv4-options"),
                P("ttl"),
                P("psd", RosPropertyClassification.ConfigOpaque),
                P("hotspot"),
                P("p2p"),
                P("realm"),
            ]);
        }

        if (includeIpv6Only)
        {
            properties.AddRange(
            [
                P("ipv6-header"),
                P("hop-limit"),
            ]);
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
