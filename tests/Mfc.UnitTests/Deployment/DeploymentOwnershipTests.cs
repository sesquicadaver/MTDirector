using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>Domain gate for AUDIT-DEP-01 ownership vs background recovery.</summary>
public sealed class DeploymentOwnershipTests
{
    private static readonly DateTimeOffset T0 =
        DateTimeOffset.Parse("2026-09-13T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void IsAbandonedWhenLockMissingOrExpiredOrDifferentDeployment()
    {
        DeploymentOperationId op = DeploymentOperationId.New();
        NodeId node = NodeId.New();
        Assert.True(DeploymentOwnership.IsAbandonedForRecovery(null, op, T0));

        DeploymentLock expired = DeploymentLock.Acquire(node, op, "owner", T0, lease: TimeSpan.FromSeconds(30));
        expired.Expire(T0.AddMinutes(1));
        Assert.True(DeploymentOwnership.IsAbandonedForRecovery(expired, op, T0.AddMinutes(1)));

        DeploymentLock other = DeploymentLock.Acquire(node, DeploymentOperationId.New(), "owner", T0);
        Assert.True(DeploymentOwnership.IsAbandonedForRecovery(other, op, T0));
    }

    [Fact]
    public void IsNotAbandonedWhenLiveLockMatchesDeployment()
    {
        DeploymentOperationId op = DeploymentOperationId.New();
        DeploymentLock live = DeploymentLock.Acquire(NodeId.New(), op, "owner", T0);
        Assert.False(DeploymentOwnership.IsAbandonedForRecovery(live, op, T0));
    }

    [Fact]
    public void AcquireForStartReplacesExpiredButRejectsLive()
    {
        NodeId node = NodeId.New();
        DeploymentOperationId first = DeploymentOperationId.New();
        DeploymentOperationId second = DeploymentOperationId.New();
        DeploymentLock expired = DeploymentLock.Acquire(node, first, "owner", T0, lease: TimeSpan.FromSeconds(30));
        expired.Expire(T0.AddMinutes(1));

        DeploymentLock replaced = DeploymentOwnership.AcquireForStart(
            node, second, "owner-b", T0.AddMinutes(2), expired);
        Assert.Equal(second, replaced.DeploymentId);
        Assert.Equal("owner-b", replaced.OwnerInstanceId);

        DeploymentLock live = DeploymentLock.Acquire(node, first, "owner", T0.AddMinutes(3));
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(
            () => DeploymentOwnership.AcquireForStart(node, second, "owner-b", T0.AddMinutes(3), live));
        Assert.Contains(DeploymentCodes.LockHeld, ex.Message, StringComparison.Ordinal);
    }
}
