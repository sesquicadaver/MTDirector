using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-46: known-limitations / queue seed locks next continuous residual tranche (W7-47 SEC-07…15).</summary>
public sealed class ContinuousResidualSeedW746LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedSecUowResidualAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-46 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("SEC-07…SEC-15", limitations, StringComparison.Ordinal);
        Assert.Contains("IUnitOfWork", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-47", roadmap, StringComparison.Ordinal);
        Assert.Contains("Lock SEC-07…SEC-15 DONE residual Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-47", plan, StringComparison.Ordinal);
        Assert.Contains("SEC-07…SEC-15", plan, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
