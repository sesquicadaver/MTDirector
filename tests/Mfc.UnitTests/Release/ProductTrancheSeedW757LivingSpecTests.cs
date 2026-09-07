using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-57: known-limitations / queue seed locks next product tranche (W7-58 PLAN-04).</summary>
public sealed class ProductTrancheSeedW757LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan04AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-57 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-04", limitations, StringComparison.Ordinal);
        Assert.Contains("contract-test", limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-58", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-04 — Inventory next contract-test", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-58", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-04", plan, StringComparison.Ordinal);
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
