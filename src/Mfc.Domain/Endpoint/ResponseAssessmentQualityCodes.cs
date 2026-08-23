namespace Mfc.Domain.Endpoint;

/// <summary>Stable finding codes for response assessment quality evaluation (M7.3-05 / next-2).</summary>
public static class ResponseAssessmentQualityCodes
{
    public const string QualityEvaluated = "RESPONSE_ASSESSMENT_QUALITY_EVALUATED";

    public const string LimitedSessionVisibility = "RESPONSE_ASSESSMENT_LIMITED_SESSION_VISIBILITY";

    public const string SessionNotObserved = "RESPONSE_ASSESSMENT_SESSION_NOT_OBSERVED";

    public const string HardwareOffloadLimitedVisibility = "RESPONSE_ASSESSMENT_HW_OFFLOAD_LIMITED_VISIBILITY";

    public const string IndeterminateRouteCertainty = "RESPONSE_ASSESSMENT_INDETERMINATE_ROUTE_CERTAINTY";

    public const string IndeterminatePacketPath = "RESPONSE_ASSESSMENT_INDETERMINATE_PACKET_PATH";

    public const string HardwareOffloadedPacketPath = "RESPONSE_ASSESSMENT_HW_OFFLOADED_PACKET_PATH";

    public const string MixedPacketPath = "RESPONSE_ASSESSMENT_MIXED_PACKET_PATH";

    public const string IndeterminateFeasibility = "RESPONSE_ASSESSMENT_INDETERMINATE_FEASIBILITY";
}
