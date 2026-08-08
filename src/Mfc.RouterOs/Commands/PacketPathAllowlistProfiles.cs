namespace Mfc.RouterOs.Commands;

/// <summary>
/// Network-significant property profiles for packet-path allowlist expansion (next-1, N1-01).
/// Environment variables, mount contents, shell/cmd/entrypoint, and secrets are never requested.
/// </summary>
internal static class PacketPathAllowlistProfiles
{
    public static RosPropertyProfile Containers { get; } = new(
        "containers",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("tag"),
            P("os"),
            P("arch"),
            P("interface"),
            P("hostname"),
            P("dns"),
            P("domain-name"),
            P("start-on-boot"),
            P("auto-restart-interval"),
            P("cpu-list"),
            P("memory-high"),
            P("memory-max"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("disabled"),
            P("status", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile Apps { get; } = new(
        "apps",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("category"),
            P("interface"),
            P("description", redaction: RosRedactionPolicy.LogRedacted),
            P("disabled"),
            P("custom", RosPropertyClassification.CapabilityTyped),
            P("from-app-store", RosPropertyClassification.CapabilityTyped),
            P("running", RosPropertyClassification.ObservationTyped),
            P("internal-address", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile VethInterfaces { get; } = new(
        "veth_interfaces",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("address"),
            P("gateway"),
            P("gateway6"),
            P("dhcp"),
            P("mac-address"),
            P("container-mac-address"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("disabled"),
            P("running", RosPropertyClassification.ObservationTyped),
            P("actual-mtu", RosPropertyClassification.ObservationTyped),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile IpVrfs { get; } = new(
        "ip_vrfs",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("interfaces"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("disabled"),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("inactive", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
        ]);

    public static RosPropertyProfile VlanInterfaces { get; } = new(
        "vlan_interfaces",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("vlan-id"),
            P("interface"),
            P("mtu"),
            P("use-service-tag"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("disabled"),
            P("running", RosPropertyClassification.ObservationTyped),
            P("actual-mtu", RosPropertyClassification.ObservationTyped),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
        ]);

    private static RosPropertyDefinition P(
        string name,
        RosPropertyClassification classification = RosPropertyClassification.ConfigTyped,
        RosRedactionPolicy redaction = RosRedactionPolicy.None)
    {
        if (string.Equals(name, "comment", StringComparison.Ordinal)
            || string.Equals(name, "note", StringComparison.Ordinal)
            || string.Equals(name, "description", StringComparison.Ordinal))
        {
            redaction = RosRedactionPolicy.LogRedacted;
        }

        return new RosPropertyDefinition(name, classification, redaction);
    }
}
