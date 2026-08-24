namespace Mfc.Domain.Incident;

/// <summary>Outbound feedback event kinds for the external analytics complex (next-2 §Зворотний зв'язок / M7.4-05).</summary>
public enum ResponseFeedbackEventKind : byte
{
    Planned = 1,
    Blocked = 2,
    Started = 3,
    Applied = 4,
    Verified = 5,
    RolledBack = 6,
    RecoveryRequired = 7,
    Expired = 8,
}
