namespace Mfc.Domain.Endpoint;

/// <summary>Invariant violation codes for endpoint presence intervals and routing context (M7.2-02).</summary>
public static class EndpointPresenceCodes
{
    public const string MissingSiteId = "ENDPOINT_PRESENCE_MISSING_SITE_ID";
    public const string MissingNodeId = "ENDPOINT_PRESENCE_MISSING_NODE_ID";
    public const string MissingSourceAddress = "ENDPOINT_PRESENCE_MISSING_SOURCE_ADDRESS";
    public const string InvalidValidityRange = "ENDPOINT_PRESENCE_INVALID_VALIDITY_RANGE";
    public const string OverlappingActiveInterval = "ENDPOINT_PRESENCE_OVERLAPPING_ACTIVE_INTERVAL";
    public const string IntervalNotActive = "ENDPOINT_PRESENCE_INTERVAL_NOT_ACTIVE";
    public const string CloseBeforeValidFrom = "ENDPOINT_PRESENCE_CLOSE_BEFORE_VALID_FROM";
}
