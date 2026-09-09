using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-136: known-limitations / queue seed locks next PLAN-13 row (W7-137 DESK-LAYOUT-05).</summary>
public sealed class ProductTrancheSeedW7136LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskLayout05AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));

        Assert.Contains("Intentional residual (W7-136 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-05", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-137", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-137", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-05 — Policies MaxHeight cascade Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-137", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-05", plan, StringComparison.Ordinal);
        Assert.Contains("W7-136 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-05", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-137", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-136", roadmap, StringComparison.Ordinal);
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
