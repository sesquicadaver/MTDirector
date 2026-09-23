namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Durable exclusive Node lock for onboarding (Onboarding Spec §§52–53 / AUDIT-OWN-01). Expired rows are retained.</summary>
public sealed class OnboardingLockEntity
{
    public Guid NodeId { get; set; }

    public Guid OperationId { get; set; }

    public required string OwnerInstanceId { get; set; }

    public DateTimeOffset AcquiredAtUtc { get; set; }

    public DateTimeOffset HeartbeatAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}
