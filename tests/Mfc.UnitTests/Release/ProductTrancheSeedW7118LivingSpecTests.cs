using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-118: known-limitations / queue seed locks next product tranche (W7-119 PLAN-12).</summary>
public sealed class ProductTrancheSeedW7118LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan12AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));

        Assert.Contains("Intentional residual (W7-118 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-12", limitations, StringComparison.Ordinal);
        Assert.Contains("after PLAN-11", limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-119", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-12", roadmap, StringComparison.Ordinal);
        Assert.Contains("Policies residual lifecycle", roadmap, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-119", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-12", plan, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan11, StringComparison.Ordinal);
        Assert.Contains("PLAN-12", plan11, StringComparison.Ordinal);
        Assert.Contains("Successor", plan11, StringComparison.Ordinal);
        Assert.Contains("W7-118", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-119", roadmap, StringComparison.Ordinal);
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
