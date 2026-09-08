using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-111: known-limitations / queue seed locks next product tranche (W7-112 PLAN-11).</summary>
public sealed class ProductTrancheSeedW7111LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan11AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));

        Assert.Contains("Intentional residual (W7-111 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-11", limitations, StringComparison.Ordinal);
        Assert.Contains("after PLAN-10", limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-112", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-11", roadmap, StringComparison.Ordinal);
        Assert.Contains("Policies review-compose lifecycle", roadmap, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-112", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-11", plan, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan10, StringComparison.Ordinal);
        Assert.Contains("PLAN-11", plan10, StringComparison.Ordinal);
        Assert.Contains("Successor", plan10, StringComparison.Ordinal);
        Assert.Contains("W7-111", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-112", roadmap, StringComparison.Ordinal);
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
