using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-104: known-limitations / queue seed locks next product tranche (W7-105 PLAN-10).</summary>
public sealed class ProductTrancheSeedW7104LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan10AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-104 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-10", limitations, StringComparison.Ordinal);
        Assert.Contains("after PLAN-09", limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-105", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-10 — Inventory next product Living Spec tranche after PLAN-09", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-105", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-10", plan, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan09, StringComparison.Ordinal);
        Assert.Contains("PLAN-10", plan09, StringComparison.Ordinal);
        Assert.Contains("Successor", plan09, StringComparison.Ordinal);
        Assert.Contains("W7-104", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-105", roadmap, StringComparison.Ordinal);
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
