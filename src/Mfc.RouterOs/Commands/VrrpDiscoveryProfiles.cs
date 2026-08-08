namespace Mfc.RouterOs.Commands;

/// <summary>Property profile for <c>/interface/vrrp/print</c> (Read Adapter Spec §33).</summary>
internal static class VrrpDiscoveryProfiles
{
    public static RosPropertyProfile VrrpInterfaces { get; } = new(
        "vrrp_interfaces",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("name"),
            P("interface"),
            P("vrid"),
            P("version"),
            P("v3-protocol"),
            P("priority"),
            P("interval"),
            P("preemption-mode"),
            P("authentication"),
            P("group-authority"),
            P("sync-connection-tracking"),
            P("connection-tracking-mode"),
            P("connection-tracking-port"),
            P("remote-address"),
            P("arp"),
            P("arp-timeout"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("disabled"),
            P("invalid", RosPropertyClassification.ObservationTyped),
            P("running", RosPropertyClassification.ObservationTyped),
            P("master", RosPropertyClassification.ObservationTyped),
            P("backup", RosPropertyClassification.ObservationTyped),
            P("failure", RosPropertyClassification.ObservationTyped),
            P("grp-authority", RosPropertyClassification.ObservationTyped),
            P("grp-member", RosPropertyClassification.ObservationTyped),
            P("mtu", RosPropertyClassification.ObservationTyped),
            // Forbidden: password, on-master, on-backup, on-fail (not requested).
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
