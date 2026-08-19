namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Durable exclusive Node lock (Safe Deployment Spec §15 / M4-01). Expired rows are retained.</summary>
public sealed class DeploymentLockEntity
{
    public Guid NodeId { get; set; }

    public Guid DeploymentId { get; set; }

    public required string OwnerInstanceId { get; set; }

    public DateTimeOffset AcquiredAtUtc { get; set; }

    public DateTimeOffset HeartbeatAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}
