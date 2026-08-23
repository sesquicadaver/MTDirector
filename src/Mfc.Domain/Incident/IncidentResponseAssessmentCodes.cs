namespace Mfc.Domain.Incident;

/// <summary>Stable finding codes for IncidentSignal ↔ ResponseAssessment contract (M7.3-06 / next-2).</summary>
public static class IncidentResponseAssessmentCodes
{
    public const string ContractBound = "INCIDENT_RESPONSE_ASSESSMENT_BOUND";

    public const string MissingCorrelationFlow = "INCIDENT_RESPONSE_ASSESSMENT_MISSING_FLOW";

    public const string SignalConfidenceExceedsAssessment = "INCIDENT_SIGNAL_CONFIDENCE_EXCEEDS_ASSESSMENT";

    public const string IncidentIdMappedFromEventId = "INCIDENT_ID_MAPPED_FROM_EVENT_ID";
}
