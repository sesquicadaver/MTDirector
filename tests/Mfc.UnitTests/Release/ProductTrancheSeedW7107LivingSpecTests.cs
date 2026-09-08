using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-107: known-limitations / queue seed locks next PLAN-10 row (W7-108 DESK-DIFF-01).</summary>
public sealed class ProductTrancheSeedW7107LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskDiff01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));

        Assert.Contains("Intentional residual (W7-107 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-DIFF-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-108", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-108", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-DIFF-01 — Desktop Policies Diff Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-108", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-DIFF-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-106 DONE", plan10, StringComparison.Ordinal);
        Assert.Contains("DESK-DIFF-01", plan10, StringComparison.Ordinal);
        Assert.Contains("W7-108", plan10, StringComparison.Ordinal);
        Assert.Contains("W7-107", roadmap, StringComparison.Ordinal);
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
