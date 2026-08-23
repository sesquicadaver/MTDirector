namespace Mfc.Domain.Incident;

/// <summary>Stable finding codes for on-demand session context (M7.3-03 / next-2 §2).</summary>
public static class IncidentSessionContextCodes
{
    public const string SessionResolved = "INCIDENT_SESSION_RESOLVED";
    public const string SessionNotFound = "INCIDENT_SESSION_NOT_FOUND";
    public const string SessionAmbiguous = "INCIDENT_SESSION_AMBIGUOUS";
    public const string HwOffloadLimitedVisibility = "INCIDENT_SESSION_HW_OFFLOAD_LIMITED";
    public const string FastTrackLimitedVisibility = "INCIDENT_SESSION_FASTTRACK_LIMITED";
    public const string MissingOriginalFlow = "INCIDENT_SESSION_MISSING_ORIGINAL_FLOW";
}
