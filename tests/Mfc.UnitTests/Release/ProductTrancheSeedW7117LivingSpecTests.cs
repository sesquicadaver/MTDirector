using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-117: known-limitations / queue seed locks next PLAN-11 row (W7-116 DESK-GATE-01).</summary>
public sealed class ProductTrancheSeedW7117LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskGate01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));

        Assert.Contains("Intentional residual (W7-117 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-GATE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-116", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-116", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-GATE-01 — Desktop Policies Approve/Bind/Compile Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-116", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-GATE-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-115 DONE", plan11, StringComparison.Ordinal);
        Assert.Contains("DESK-GATE-01", plan11, StringComparison.Ordinal);
        Assert.Contains("W7-116", plan11, StringComparison.Ordinal);
        Assert.Contains("W7-117", roadmap, StringComparison.Ordinal);
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
