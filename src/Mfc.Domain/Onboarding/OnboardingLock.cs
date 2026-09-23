using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>Durable exclusive Node lock for onboarding write vs recovery (Onboarding Spec §§52–53 / AUDIT-OWN-01).</summary>
public sealed class OnboardingLock
{
    private OnboardingLock(
        NodeId nodeId,
        OnboardingOperationId operationId,
        string ownerInstanceId,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset heartbeatAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        NodeId = nodeId;
        OperationId = operationId;
        OwnerInstanceId = ownerInstanceId;
        AcquiredAtUtc = acquiredAtUtc;
        HeartbeatAtUtc = heartbeatAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public NodeId NodeId { get; }

    public OnboardingOperationId OperationId { get; }

    public string OwnerInstanceId { get; }

    public DateTimeOffset AcquiredAtUtc { get; }

    public DateTimeOffset HeartbeatAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc.ToUniversalTime() >= ExpiresAtUtc;

    public static OnboardingLock Acquire(
        NodeId nodeId,
        OnboardingOperationId operationId,
        string ownerInstanceId,
        DateTimeOffset nowUtc,
        TimeSpan? lease = null,
        OnboardingLock? existing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerInstanceId);
        DateTimeOffset now = nowUtc.ToUniversalTime();
        TimeSpan ttl = lease ?? OnboardingCodes.DefaultLockLease;
        if (ttl < TimeSpan.FromSeconds(30) || ttl > TimeSpan.FromMinutes(10))
        {
            throw new DomainInvariantException("onboarding lock lease must be between 30s and 10m.");
        }

        if (existing is not null)
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.LockHeld}: Node already has an onboarding lock; expired locks require recovery inspection.");
        }

        return new OnboardingLock(
            nodeId,
            operationId,
            ownerInstanceId.Trim(),
            now,
            now,
            now + ttl);
    }

    public static OnboardingLock Reconstitute(
        NodeId nodeId,
        OnboardingOperationId operationId,
        string ownerInstanceId,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset heartbeatAtUtc,
        DateTimeOffset expiresAtUtc)
        => new(
            nodeId,
            operationId,
            ownerInstanceId,
            acquiredAtUtc.ToUniversalTime(),
            heartbeatAtUtc.ToUniversalTime(),
            expiresAtUtc.ToUniversalTime());

    public void Heartbeat(string ownerInstanceId, DateTimeOffset nowUtc, TimeSpan? lease = null)
    {
        if (!string.Equals(OwnerInstanceId, ownerInstanceId, StringComparison.Ordinal))
        {
            throw new DomainInvariantException($"{OnboardingCodes.LockOwnerMismatch}: heartbeat owner mismatch.");
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        TimeSpan ttl = lease ?? OnboardingCodes.DefaultLockLease;
        HeartbeatAtUtc = now;
        ExpiresAtUtc = now + ttl;
    }

    public void EnsureOwner(string ownerInstanceId)
    {
        if (!string.Equals(OwnerInstanceId, ownerInstanceId, StringComparison.Ordinal))
        {
            throw new DomainInvariantException($"{OnboardingCodes.LockOwnerMismatch}: lock owner mismatch.");
        }
    }

    /// <summary>
    /// Ends the live lease immediately (row retained). Used when the owning Start call leaves Execute.
    /// </summary>
    public void Expire(DateTimeOffset nowUtc)
    {
        DateTimeOffset now = nowUtc.ToUniversalTime();
        if (now < AcquiredAtUtc)
        {
            now = AcquiredAtUtc;
        }

        HeartbeatAtUtc = now;
        ExpiresAtUtc = now;
    }
}
