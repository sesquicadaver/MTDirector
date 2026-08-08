namespace Mfc.RouterOs.Commands;

/// <summary>Property profiles for bridge/VLAN/switch reads (Spec §34–35, M1-16).</summary>
internal static class BridgeSwitchDiscoveryProfiles
{
    public static RosPropertyProfile Bridges { get; } = new(
        "bridges",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("admin-mac"),
            P("auto-mac"),
            P("ageing-time"),
            P("arp"),
            P("arp-timeout"),
            P("protocol-mode"),
            P("priority"),
            P("pvid"),
            P("vlan-filtering"),
            P("frame-types"),
            P("ingress-filtering"),
            P("dhcp-snooping"),
            P("igmp-snooping"),
            P("fast-forward"),
            P("mtu"),
            P("disabled"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("running", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
            P("mac-address", RosPropertyClassification.ObservationTyped),
            P("actual-mtu", RosPropertyClassification.ObservationTyped),
            P("l2mtu", RosPropertyClassification.ObservationTyped),
            P("root-bridge", RosPropertyClassification.ObservationTyped),
            P("root-port", RosPropertyClassification.ObservationTyped),
            P("root-path-cost", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile BridgePorts { get; } = new(
        "bridge_ports",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("bridge"),
            P("interface"),
            P("pvid"),
            P("frame-types"),
            P("ingress-filtering"),
            P("tag-stacking"),
            P("horizon"),
            P("hw"),
            P("path-cost"),
            P("internal-path-cost"),
            P("priority"),
            P("edge"),
            P("point-to-point"),
            P("learn"),
            P("flood-unknown-unicast"),
            P("multicast-router"),
            P("restricted-role"),
            P("restricted-tcn"),
            P("bpdu-guard"),
            P("trusted"),
            P("disabled"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("inactive", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
            P("hw-offload", RosPropertyClassification.ObservationTyped),
            P("role", RosPropertyClassification.ObservationTyped),
            P("root-path-cost", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile BridgeSettings { get; } = new(
        "bridge_settings",
        [
            P("use-ip-firewall"),
            P("use-ip-firewall-for-vlan"),
            P("use-ip-firewall-for-pppoe"),
            P("allow-fast-path"),
            P("bridge-fast-path-active", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile BridgeVlans { get; } = new(
        "bridge_vlans",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("bridge"),
            P("vlan-ids"),
            P("tagged"),
            P("untagged"),
            P("disabled"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("current-tagged", RosPropertyClassification.ObservationTyped),
            P("current-untagged", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile EthernetSwitches { get; } = new(
        "ethernet_switches",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name", RosPropertyClassification.CapabilityTyped),
            P("type", RosPropertyClassification.CapabilityTyped),
            P("l3-hw-offloading"),
        ]);

    public static RosPropertyProfile EthernetSwitchPorts { get; } = new(
        "ethernet_switch_ports",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("switch"),
            P("default-vlan-id"),
            P("vlan-mode"),
            P("vlan-header"),
            P("l3-hw-offloading"),
        ]);

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
