namespace Mfc.Domain.Incident;

/// <summary>Stable finding codes for sensor observation ↔ route trace correlation (M7.3-04 / M7.1 §16).</summary>
public static class SensorObservationCorrelationCodes
{
    public const string CorrelationAligned = "SENSOR_OBSERVATION_ALIGNED";

    public const string NoRouteTrace = "NO_ROUTE_TRACE";

    public const string MissingOriginalFlow = "MISSING_ORIGINAL_FLOW";

    public const string MissingTranslatedFlow = "MISSING_TRANSLATED_FLOW";

    public const string IngressInterfaceMismatch = "INGRESS_INTERFACE_MISMATCH";

    public const string EgressInterfaceMismatch = "EGRESS_INTERFACE_MISMATCH";

    public const string VrfMismatch = "VRF_MISMATCH";

    public const string RoutingMarkMismatch = "ROUTING_MARK_MISMATCH";

    public const string RoutingTableMismatch = "ROUTING_TABLE_MISMATCH";

    public const string FlowDestinationMismatch = "FLOW_DESTINATION_MISMATCH";

    public const string SensorBypassHwOffload = "SENSOR_BYPASS_HW_OFFLOAD";

    public const string InsufficientObservationContext = "INSUFFICIENT_OBSERVATION_CONTEXT";
}
