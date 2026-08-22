namespace Mfc.Domain.Incident;

/// <summary>Stable finding codes for historical active-state resolution (M7.3-02 / next-2 §4).</summary>
public static class ActiveStateIntervalCodes
{
    public const string NoTimelineData = "ACTIVE_STATE_NO_TIMELINE_DATA";
    public const string OccurredBeforeFirstTransition = "ACTIVE_STATE_OCCURRED_BEFORE_FIRST_TRANSITION";
    public const string DeviceMismatch = "ACTIVE_STATE_DEVICE_MISMATCH";
    public const string DuplicateTransitionInstant = "ACTIVE_STATE_DUPLICATE_TRANSITION_INSTANT";
    public const string NonMonotonicTimeline = "ACTIVE_STATE_NON_MONOTONIC_TIMELINE";
    public const string Resolved = "ACTIVE_STATE_RESOLVED";
}
