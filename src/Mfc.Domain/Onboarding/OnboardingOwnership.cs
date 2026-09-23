using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Ownership gate for onboarding write vs background recovery (Onboarding Spec §§52–53 / AUDIT-OWN-01).
/// </summary>
public static class OnboardingOwnership
{
    public const string DefaultOwnerInstanceId = "mfc-controller";

    public const string RecoverySkippedLockHeld = "ONBOARDING_LOCK_HELD";

    /// <summary>
    /// True when recovery may mutate the operation (no live lease for this onboarding).
    /// </summary>
    public static bool IsAbandonedForRecovery(
        OnboardingLock? onboardingLock,
        OnboardingOperationId operationId,
        DateTimeOffset nowUtc)
    {
        if (onboardingLock is null)
        {
            return true;
        }

        if (onboardingLock.IsExpired(nowUtc))
        {
            return true;
        }

        return onboardingLock.OperationId.Value != operationId.Value;
    }

    /// <summary>
    /// Acquires a new lock, or replaces an expired row for the same Node (expired locks are not stolen while live).
    /// </summary>
    public static OnboardingLock AcquireForStart(
        NodeId nodeId,
        OnboardingOperationId operationId,
        string ownerInstanceId,
        DateTimeOffset nowUtc,
        OnboardingLock? existing = null,
        TimeSpan? lease = null)
    {
        if (existing is null)
        {
            return OnboardingLock.Acquire(nodeId, operationId, ownerInstanceId, nowUtc, lease);
        }

        if (!existing.IsExpired(nowUtc))
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.LockHeld}: Node already has a live onboarding lock.");
        }

        if (existing.NodeId.Value != nodeId.Value)
        {
            throw new DomainInvariantException("Cannot replace a lock for a different Node.");
        }

        return OnboardingLock.Acquire(nodeId, operationId, ownerInstanceId, nowUtc, lease, existing: null);
    }
}
