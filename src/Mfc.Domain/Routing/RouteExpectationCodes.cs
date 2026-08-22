namespace Mfc.Domain.Routing;

/// <summary>Finding codes for <see cref="RouteExpectationEvaluator"/> (M7.1 Spec §11).</summary>
public static class RouteExpectationCodes
{
    public const string ExpectedVrfMismatch = "ROUTE_EXPECTATION_EXPECTED_VRF_MISMATCH";

    public const string ExpectedVrfMismatchCritical = "ROUTE_EXPECTATION_EXPECTED_VRF_MISMATCH_CRITICAL";

    public const string ExpectedTableMismatch = "ROUTE_EXPECTATION_EXPECTED_TABLE_MISMATCH";

    public const string ExpectedTableMismatchCritical = "ROUTE_EXPECTATION_EXPECTED_TABLE_MISMATCH_CRITICAL";

    public const string AllowedNextHopViolation = "ROUTE_EXPECTATION_ALLOWED_NEXT_HOP_VIOLATION";

    public const string AllowedNextHopViolationCritical = "ROUTE_EXPECTATION_ALLOWED_NEXT_HOP_VIOLATION_CRITICAL";

    public const string AllowedEgressInterfaceViolation = "ROUTE_EXPECTATION_ALLOWED_EGRESS_INTERFACE_VIOLATION";

    public const string AllowedEgressInterfaceViolationCritical =
        "ROUTE_EXPECTATION_ALLOWED_EGRESS_INTERFACE_VIOLATION_CRITICAL";

    public const string AllowedEgressZoneViolation = "ROUTE_EXPECTATION_ALLOWED_EGRESS_ZONE_VIOLATION";

    public const string AllowedEgressZoneViolationCritical =
        "ROUTE_EXPECTATION_ALLOWED_EGRESS_ZONE_VIOLATION_CRITICAL";

    public const string RequiredRouteTypeMissing = "ROUTE_EXPECTATION_REQUIRED_ROUTE_TYPE_MISSING";

    public const string RequiredRouteTypeMissingCritical = "ROUTE_EXPECTATION_REQUIRED_ROUTE_TYPE_MISSING_CRITICAL";

    public const string ForbiddenRouteTypePresent = "ROUTE_EXPECTATION_FORBIDDEN_ROUTE_TYPE_PRESENT";

    public const string ForbiddenRouteTypePresentCritical = "ROUTE_EXPECTATION_FORBIDDEN_ROUTE_TYPE_PRESENT_CRITICAL";

    public const string CpuFirewallPathRequired = "ROUTE_EXPECTATION_CPU_FIREWALL_PATH_REQUIRED";

    public const string CpuFirewallPathRequiredCritical = "ROUTE_EXPECTATION_CPU_FIREWALL_PATH_REQUIRED_CRITICAL";

    public const string ReversePathMissing = "ROUTE_EXPECTATION_REVERSE_PATH_MISSING";

    public const string ReversePathMissingCritical = "ROUTE_EXPECTATION_REVERSE_PATH_MISSING_CRITICAL";

    public const string AsymmetricReversePathUnexpected = "ROUTE_EXPECTATION_ASYMMETRIC_REVERSE_PATH_UNEXPECTED";

    public const string AsymmetricReversePathUnexpectedCritical =
        "ROUTE_EXPECTATION_ASYMMETRIC_REVERSE_PATH_UNEXPECTED_CRITICAL";
}
