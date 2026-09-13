using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Ownership gate for deployment write vs background recovery (Safe Deployment Spec §15 / AUDIT-DEP-01).
/// </summary>
public static class DeploymentOwnership
{
    public const string DefaultOwnerInstanceId = "mfc-controller";

    public const string RecoverySkippedLockHeld = "DEPLOYMENT_LOCK_HELD";

    /// <summary>
    /// True when recovery may mutate the operation (no live lease for this deployment).
    /// </summary>
    public static bool IsAbandonedForRecovery(
        DeploymentLock? deploymentLock,
        DeploymentOperationId operationId,
        DateTimeOffset nowUtc)
    {
        if (deploymentLock is null)
        {
            return true;
        }

        if (deploymentLock.IsExpired(nowUtc))
        {
            return true;
        }

        return deploymentLock.DeploymentId.Value != operationId.Value;
    }

    /// <summary>
    /// Acquires a new lock, or replaces an expired row for the same Node (expired locks are not stolen while live).
    /// </summary>
    public static DeploymentLock AcquireForStart(
        NodeId nodeId,
        DeploymentOperationId deploymentId,
        string ownerInstanceId,
        DateTimeOffset nowUtc,
        DeploymentLock? existing = null,
        TimeSpan? lease = null)
    {
        if (existing is null)
        {
            return DeploymentLock.Acquire(nodeId, deploymentId, ownerInstanceId, nowUtc, lease);
        }

        if (!existing.IsExpired(nowUtc))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.LockHeld}: Node already has a live deployment lock.");
        }

        if (existing.NodeId.Value != nodeId.Value)
        {
            throw new DomainInvariantException("Cannot replace a lock for a different Node.");
        }

        return DeploymentLock.Acquire(nodeId, deploymentId, ownerInstanceId, nowUtc, lease, existing: null);
    }
}
