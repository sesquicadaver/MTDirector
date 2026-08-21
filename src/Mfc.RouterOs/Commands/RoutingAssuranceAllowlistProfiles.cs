namespace Mfc.RouterOs.Commands;

/// <summary>
/// Property profiles for routing-assurance allowlist expansion (M7.1-01 / Spec §3).
/// Secrets and VPN peer credentials are never requested.
/// </summary>
internal static class RoutingAssuranceAllowlistProfiles
{
    /// <summary>
    /// Routing decision order and check-gateway knobs (<c>/routing/settings</c>).
    /// <c>policy-rules</c> is the authoritative decision-stage list (Spec §2–§5).
    /// </summary>
    public static RosPropertyProfile RoutingSettings { get; } = new(
        "routing_settings",
        [
            P("policy-rules"),
            P("check-gateway-ping-count"),
            P("check-gateway-ping-interval"),
            P("check-gateway-ping-timeout"),
            P("connected-in-chain"),
            P("dynamic-in-chain"),
            P("single-process"),
        ]);

    /// <summary>
    /// Route filter rules that accept/reject/modify attributes before selection (Spec §10).
    /// <c>rule</c> is opaque script-like syntax — not expanded here.
    /// </summary>
    public static RosPropertyProfile RoutingFilterRules { get; } = new(
        "routing_filter_rules",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("chain"),
            P("rule", RosPropertyClassification.ConfigOpaque),
            P("disabled"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("inactive", RosPropertyClassification.ObservationTyped),
            P("invalid", RosPropertyClassification.ObservationTyped),
        ]);

    /// <summary>
    /// Route select-rules that pick competing candidates (Spec §7 / §10).
    /// </summary>
    public static RosPropertyProfile RoutingFilterSelectRules { get; } = new(
        "routing_filter_select_rules",
        [
            P(".id", RosPropertyClassification.RawOnly),
            P("chain"),
            P("rule", RosPropertyClassification.ConfigOpaque),
            P("disabled"),
            P("comment", redaction: RosRedactionPolicy.LogRedacted),
            P("dynamic", RosPropertyClassification.ObservationTyped),
            P("inactive", RosPropertyClassification.ObservationTyped),
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
