using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>Domain gate for AUDIT-OWN-01 ownership vs background recovery.</summary>
public sealed class OnboardingOwnershipTests
{
    private static readonly DateTimeOffset T0 =
        DateTimeOffset.Parse("2026-09-23T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void IsAbandonedWhenLockMissingOrExpiredOrDifferentOperation()
    {
        OnboardingOperationId op = OnboardingOperationId.New();
        NodeId node = NodeId.New();
        Assert.True(OnboardingOwnership.IsAbandonedForRecovery(null, op, T0));

        OnboardingLock expired = OnboardingLock.Acquire(node, op, "owner", T0, lease: TimeSpan.FromSeconds(30));
        expired.Expire(T0.AddMinutes(1));
        Assert.True(OnboardingOwnership.IsAbandonedForRecovery(expired, op, T0.AddMinutes(1)));

        OnboardingLock other = OnboardingLock.Acquire(node, OnboardingOperationId.New(), "owner", T0);
        Assert.True(OnboardingOwnership.IsAbandonedForRecovery(other, op, T0));
    }

    [Fact]
    public void IsNotAbandonedWhenLiveLockMatchesOperation()
    {
        OnboardingOperationId op = OnboardingOperationId.New();
        OnboardingLock live = OnboardingLock.Acquire(NodeId.New(), op, "owner", T0);
        Assert.False(OnboardingOwnership.IsAbandonedForRecovery(live, op, T0));
    }

    [Fact]
    public void AcquireForStartReplacesExpiredButRejectsLive()
    {
        NodeId node = NodeId.New();
        OnboardingOperationId first = OnboardingOperationId.New();
        OnboardingOperationId second = OnboardingOperationId.New();
        OnboardingLock expired = OnboardingLock.Acquire(node, first, "owner", T0, lease: TimeSpan.FromSeconds(30));
        expired.Expire(T0.AddMinutes(1));

        OnboardingLock replaced = OnboardingOwnership.AcquireForStart(
            node, second, "owner-b", T0.AddMinutes(2), expired);
        Assert.Equal(second, replaced.OperationId);
        Assert.Equal("owner-b", replaced.OwnerInstanceId);

        OnboardingLock live = OnboardingLock.Acquire(node, first, "owner", T0.AddMinutes(3));
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(
            () => OnboardingOwnership.AcquireForStart(node, second, "owner-b", T0.AddMinutes(3), live));
        Assert.Contains(OnboardingCodes.LockHeld, ex.Message, StringComparison.Ordinal);
    }
}
