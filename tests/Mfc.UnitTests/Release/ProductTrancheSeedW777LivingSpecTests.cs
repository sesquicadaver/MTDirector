using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-77: known-limitations / queue seed locks next product tranche (W7-78 PLAN-07).</summary>
public sealed class ProductTrancheSeedW777LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan07AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan06 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-06-incident-desktop-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-77 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-07", limitations, StringComparison.Ordinal);
        Assert.Contains("after PLAN-06", limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-78", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-07 — Inventory next Core MVP Desktop operator-surface Living Spec product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-78", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-07", plan, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan06, StringComparison.Ordinal);
        Assert.Contains("PLAN-07", plan06, StringComparison.Ordinal);
        Assert.Contains("Successor", plan06, StringComparison.Ordinal);
        Assert.Contains("W7-77", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-78", roadmap, StringComparison.Ordinal);
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
