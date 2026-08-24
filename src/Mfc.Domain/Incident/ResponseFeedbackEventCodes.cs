namespace Mfc.Domain.Incident;

/// <summary>Stable string codes for RESPONSE_* feedback events (next-2 / M7.4-05).</summary>
public static class ResponseFeedbackEventCodes
{
    public const string Planned = "RESPONSE_PLANNED";

    public const string Blocked = "RESPONSE_BLOCKED";

    public const string Started = "RESPONSE_STARTED";

    public const string Applied = "RESPONSE_APPLIED";

    public const string Verified = "RESPONSE_VERIFIED";

    public const string RolledBack = "RESPONSE_ROLLED_BACK";

    public const string RecoveryRequired = "RESPONSE_RECOVERY_REQUIRED";

    public const string Expired = "RESPONSE_EXPIRED";

    public static string ForKind(ResponseFeedbackEventKind kind)
        => kind switch
        {
            ResponseFeedbackEventKind.Planned => Planned,
            ResponseFeedbackEventKind.Blocked => Blocked,
            ResponseFeedbackEventKind.Started => Started,
            ResponseFeedbackEventKind.Applied => Applied,
            ResponseFeedbackEventKind.Verified => Verified,
            ResponseFeedbackEventKind.RolledBack => RolledBack,
            ResponseFeedbackEventKind.RecoveryRequired => RecoveryRequired,
            ResponseFeedbackEventKind.Expired => Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown feedback event kind."),
        };
}
