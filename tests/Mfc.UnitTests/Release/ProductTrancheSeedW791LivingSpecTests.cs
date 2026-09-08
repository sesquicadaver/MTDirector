using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-91: known-limitations / queue seed locks next PLAN-08 row (W7-92 DESK-NBR-01).</summary>
public sealed class ProductTrancheSeedW791LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskNbr01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-91 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-NBR-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-92", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-92", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-NBR-01 — Desktop Neighbor candidates Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-92", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-NBR-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-90 DONE", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-NBR-01", plan08, StringComparison.Ordinal);
        Assert.Contains("W7-92", plan08, StringComparison.Ordinal);
        Assert.Contains("W7-91", roadmap, StringComparison.Ordinal);
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
