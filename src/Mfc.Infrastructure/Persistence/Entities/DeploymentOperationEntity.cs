namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Node deployment operation with closed state machine (Safe Deployment Spec §13 / M4-01).</summary>
public sealed class DeploymentOperationEntity
{
    public const short CommittedState = 9;

    public const short RolledBackState = 12;

    public const short BlockedState = 13;

    public const short NoChangesState = 14;

    public const short CanceledState = 15;

    public const short FailedState = 16;

    public const short RecoveryRequiredState = 17;

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
        => state is CommittedState
            or RolledBackState
            or BlockedState
            or NoChangesState
            or CanceledState
            or FailedState
            or RecoveryRequiredState;
}
