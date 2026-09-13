using Mfc.Domain;
using Mfc.Domain.Deployment;
using Xunit;

namespace Mfc.UnitTests.Deployment;

public sealed class WatchdogTimeBudgetTests
{
    [Fact]
    public void RemainingStartsNearFullTtl()
    {
        WatchdogTimeBudget budget = new(TimeSpan.FromMinutes(10));
        Assert.True(budget.Remaining <= TimeSpan.FromMinutes(10));
        Assert.True(budget.Remaining > TimeSpan.FromMinutes(9));
    }

    [Fact]
    public void RemainingNeverNegativeAfterTtlElapses()
    {
        WatchdogTimeBudget budget = new(TimeSpan.FromMilliseconds(1));
        Thread.Sleep(20);
        Assert.Equal(TimeSpan.Zero, budget.Remaining);
    }

    [Fact]
    public void RejectsNegativeTtl()
    {
        Assert.Throws<DomainInvariantException>(() => new WatchdogTimeBudget(TimeSpan.FromSeconds(-1)));
    }
}
