using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-109: known-limitations / queue seed locks next PLAN-10 row (W7-110 DESK-REORDER-01).</summary>
public sealed class ProductTrancheSeedW7109LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskReorder01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));

        Assert.Contains("Intentional residual (W7-109 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-REORDER-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-110", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-110", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-REORDER-01 — Desktop Policies Move up/down Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-110", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-REORDER-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-108 DONE", plan10, StringComparison.Ordinal);
        Assert.Contains("DESK-REORDER-01", plan10, StringComparison.Ordinal);
        Assert.Contains("W7-110", plan10, StringComparison.Ordinal);
        Assert.Contains("W7-109", roadmap, StringComparison.Ordinal);
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
