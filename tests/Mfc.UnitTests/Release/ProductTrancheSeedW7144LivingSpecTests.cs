using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-144: known-limitations / queue seed locks next PLAN-13 row (W7-145 DESK-LAYOUT-09).</summary>
public sealed class ProductTrancheSeedW7144LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskLayout09AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));

        Assert.Contains("Intentional residual (W7-144 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-09", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-145", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-145", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-09 — Inventory/Zones MaxHeight frames Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-145", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-09", plan, StringComparison.Ordinal);
        Assert.Contains("W7-144 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-09", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-145", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-144", roadmap, StringComparison.Ordinal);
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
