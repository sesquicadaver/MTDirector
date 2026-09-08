using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-88: known-limitations / queue seed locks next product tranche (W7-89 PLAN-08).</summary>
public sealed class ProductTrancheSeedW788LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan08AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-88 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-08", limitations, StringComparison.Ordinal);
        Assert.Contains("after PLAN-07", limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-89", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-08 — Inventory next Desktop secondary operator-surface Living Spec product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-89", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-08", plan, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan07, StringComparison.Ordinal);
        Assert.Contains("PLAN-08", plan07, StringComparison.Ordinal);
        Assert.Contains("Successor", plan07, StringComparison.Ordinal);
        Assert.Contains("W7-88", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-89", roadmap, StringComparison.Ordinal);
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
