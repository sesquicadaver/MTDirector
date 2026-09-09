using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-130: known-limitations / queue seed locks next PLAN-13 row (W7-131 DESK-LAYOUT-02).</summary>
public sealed class ProductTrancheSeedW7130LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskLayout02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));

        Assert.Contains("Intentional residual (W7-130 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-131", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-131", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-02 — Semantic Diff entry list + splitter Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-131", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-130 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-02", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-131", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-130", roadmap, StringComparison.Ordinal);
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
