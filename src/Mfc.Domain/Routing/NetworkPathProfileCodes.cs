namespace Mfc.Domain.Routing;

/// <summary>Finding codes for <see cref="NetworkPathLatencyEvaluator"/> (M7.1 Spec §13).</summary>
public static class NetworkPathProfileCodes
{
    public const string ExpectedRoutePrefixMismatch = "NETWORK_PATH_EXPECTED_ROUTE_PREFIX_MISMATCH";

    public const string ExpectedRoutePrefixMismatchCritical = "NETWORK_PATH_EXPECTED_ROUTE_PREFIX_MISMATCH_CRITICAL";

    public const string ExpectedNextHopMismatch = "NETWORK_PATH_EXPECTED_NEXT_HOP_MISMATCH";

    public const string ExpectedNextHopMismatchCritical = "NETWORK_PATH_EXPECTED_NEXT_HOP_MISMATCH_CRITICAL";

    public const string ExpectedEgressInterfaceMismatch = "NETWORK_PATH_EXPECTED_EGRESS_INTERFACE_MISMATCH";

    public const string ExpectedEgressInterfaceMismatchCritical =
        "NETWORK_PATH_EXPECTED_EGRESS_INTERFACE_MISMATCH_CRITICAL";

    public const string ExpectedExecutionPathMismatch = "NETWORK_PATH_EXPECTED_EXECUTION_PATH_MISMATCH";

    public const string ExpectedExecutionPathMismatchCritical =
        "NETWORK_PATH_EXPECTED_EXECUTION_PATH_MISMATCH_CRITICAL";

    public const string LatencyLossHigh = "NETWORK_PATH_LATENCY_LOSS_HIGH";

    public const string LatencyLossHighCritical = "NETWORK_PATH_LATENCY_LOSS_HIGH_CRITICAL";

    public const string LatencyRttHigh = "NETWORK_PATH_LATENCY_RTT_HIGH";

    public const string LatencyRttHighCritical = "NETWORK_PATH_LATENCY_RTT_HIGH_CRITICAL";

    public const string LatencyJitterHigh = "NETWORK_PATH_LATENCY_JITTER_HIGH";

    public const string LatencyJitterHighCritical = "NETWORK_PATH_LATENCY_JITTER_HIGH_CRITICAL";

    public const string RoutePathChangedWithLatencyRegression = "ROUTE_PATH_CHANGED_WITH_LATENCY_REGRESSION";

    public const string RoutePathChangedWithLatencyRegressionCritical =
        "ROUTE_PATH_CHANGED_WITH_LATENCY_REGRESSION_CRITICAL";
}
