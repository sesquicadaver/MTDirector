using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>Durable exclusive Node lock (Safe Deployment Spec §15). Expired rows are not auto-deleted.</summary>
public sealed class DeploymentLock
{
    private DeploymentLock(
        NodeId nodeId,
        DeploymentOperationId deploymentId,
        string ownerInstanceId,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset heartbeatAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        NodeId = nodeId;
        DeploymentId = deploymentId;
        OwnerInstanceId = ownerInstanceId;
        AcquiredAtUtc = acquiredAtUtc;
        HeartbeatAtUtc = heartbeatAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public NodeId NodeId { get; }

    public DeploymentOperationId DeploymentId { get; }

    public string OwnerInstanceId { get; }

    public DateTimeOffset AcquiredAtUtc { get; }

    public DateTimeOffset HeartbeatAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc.ToUniversalTime() >= ExpiresAtUtc;

    public static DeploymentLock Acquire(
        NodeId nodeId,
        DeploymentOperationId deploymentId,
        string ownerInstanceId,
        DateTimeOffset nowUtc,
        TimeSpan? lease = null,
        DeploymentLock? existing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerInstanceId);
        DateTimeOffset now = nowUtc.ToUniversalTime();
        TimeSpan ttl = lease ?? DeploymentCodes.DefaultLockLease;
        if (ttl < TimeSpan.FromSeconds(30) || ttl > TimeSpan.FromMinutes(10))
        {
            throw new DomainInvariantException("deployment lock lease must be between 30s and 10m.");
        }

        if (existing is not null)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.LockHeld}: Node already has a deployment lock; expired locks require recovery inspection.");
        }

        return new DeploymentLock(
            nodeId,
            deploymentId,
            ownerInstanceId.Trim(),
            now,
            now,
            now + ttl);
    }

    public static DeploymentLock Reconstitute(
        NodeId nodeId,
        DeploymentOperationId deploymentId,
        string ownerInstanceId,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset heartbeatAtUtc,
        DateTimeOffset expiresAtUtc)
        => new(
            nodeId,
            deploymentId,
            ownerInstanceId,
            acquiredAtUtc.ToUniversalTime(),
            heartbeatAtUtc.ToUniversalTime(),
            expiresAtUtc.ToUniversalTime());

    public void Heartbeat(string ownerInstanceId, DateTimeOffset nowUtc, TimeSpan? lease = null)
    {
        if (!string.Equals(OwnerInstanceId, ownerInstanceId, StringComparison.Ordinal))
        {
            throw new DomainInvariantException($"{DeploymentCodes.LockOwnerMismatch}: heartbeat owner mismatch.");
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        TimeSpan ttl = lease ?? DeploymentCodes.DefaultLockLease;
        HeartbeatAtUtc = now;
        ExpiresAtUtc = now + ttl;
    }

    public void EnsureOwner(string ownerInstanceId)
    {
        if (!string.Equals(OwnerInstanceId, ownerInstanceId, StringComparison.Ordinal))
        {
            throw new DomainInvariantException($"{DeploymentCodes.LockOwnerMismatch}: lock owner mismatch.");
        }
    }
}
