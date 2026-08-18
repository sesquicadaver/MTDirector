namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Onboarding operation with closed state machine (Onboarding Spec §5 / M5-01).</summary>
public sealed class OnboardingOperationEntity
{
    public const short CommittedState = 8;

    public const short RolledBackState = 11;

    public const short BlockedState = 12;

    public const short RecoveryRequiredState = 13;

    public Guid Id { get; set; }

    public Guid NodeId { get; set; }

    public Guid PlanId { get; set; }

    public short State { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? ErrorCode { get; set; }

    public long RowVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static bool IsTerminal(short state)
        => state is CommittedState or RolledBackState or BlockedState or RecoveryRequiredState;
}
