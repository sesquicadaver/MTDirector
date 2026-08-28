namespace Mfc.RouterOs.Commands;

/// <summary>Property profiles for on-demand <c>/ip/neighbor</c> reads (#314).</summary>
internal static class NeighborDiscoveryAllowlistProfiles
{
    public static RosPropertyProfile IpNeighbors { get; } = new(
        "ip_neighbors",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("address", RosPropertyClassification.ObservationTyped),
            P("mac-address", RosPropertyClassification.ObservationTyped),
            P("identity", RosPropertyClassification.ObservationTyped),
            P("platform", RosPropertyClassification.ObservationTyped),
            P("version", RosPropertyClassification.ObservationTyped),
            P("board", RosPropertyClassification.ObservationTyped),
            P("interface", RosPropertyClassification.ObservationTyped),
            P("age", RosPropertyClassification.ObservationTyped),
        ]);

    private static RosPropertyDefinition P(
        string name,
        RosPropertyClassification classification = RosPropertyClassification.ConfigTyped,
        RosRedactionPolicy redaction = RosRedactionPolicy.None)
        => new(name, classification, redaction);
}
