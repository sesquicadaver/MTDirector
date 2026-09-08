using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-97: known-limitations / queue seed locks next product tranche (W7-98 PLAN-09).</summary>
public sealed class ProductTrancheSeedW797LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan09AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-97 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-09", limitations, StringComparison.Ordinal);
        Assert.Contains("after PLAN-08", limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-98", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "PLAN-09 — Inventory next Desktop connection-status operator-surface Living Spec product tranche",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("W7-98", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-09", plan, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan08, StringComparison.Ordinal);
        Assert.Contains("PLAN-09", plan08, StringComparison.Ordinal);
        Assert.Contains("Successor", plan08, StringComparison.Ordinal);
        Assert.Contains("W7-97", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-98", roadmap, StringComparison.Ordinal);
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
