using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-113: known-limitations / queue seed locks next PLAN-11 row (W7-115 DESK-COMPOSE-01).</summary>
public sealed class ProductTrancheSeedW7113LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskCompose01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));

        Assert.Contains("Intentional residual (W7-113 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-COMPOSE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-115", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-115", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-COMPOSE-01 — Desktop Policies Compose+RecordAnalysis Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-115", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-COMPOSE-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-114 DONE", plan11, StringComparison.Ordinal);
        Assert.Contains("DESK-COMPOSE-01", plan11, StringComparison.Ordinal);
        Assert.Contains("W7-115", plan11, StringComparison.Ordinal);
        Assert.Contains("W7-113", roadmap, StringComparison.Ordinal);
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
