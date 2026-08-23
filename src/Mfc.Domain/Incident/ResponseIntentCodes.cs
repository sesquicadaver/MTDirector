namespace Mfc.Domain.Incident;

/// <summary>Stable codes for ResponseIntent ingress and feasibility (M7.4-02 / next-2).</summary>
public static class ResponseIntentCodes
{
    public const string TemporaryDenyRequiresExpiry = "RESPONSE_INTENT_TEMPORARY_DENY_REQUIRES_EXPIRY";

    public const string TemporaryDenyRequiresSelector = "RESPONSE_INTENT_TEMPORARY_DENY_REQUIRES_SELECTOR";

    public const string NonDenyActionFullyEnforceable = "RESPONSE_INTENT_NON_DENY_FULLY_ENFORCEABLE";

    public const string L2BridgeVlanNotEnforceable = "RESPONSE_INTENT_L2_BRIDGE_VLAN_NOT_ENFORCEABLE";

    public const string ContainerForwardProven = "RESPONSE_INTENT_CONTAINER_FORWARD_PROVEN";

    public const string FastTrackLimitsToNewConnections = "RESPONSE_INTENT_FASTTRACK_LIMITS_NEW_CONNECTIONS";

    public const string MatrixClassified = "RESPONSE_INTENT_FEASIBILITY_MATRIX_CLASSIFIED";
}
