using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-50: known-limitations / queue seed locks next product tranche (W7-51 PLAN-03).</summary>
public sealed class ProductTrancheSeedW750LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan03AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-50 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-03", limitations, StringComparison.Ordinal);
        Assert.Contains("quality-gate", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-51", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-03 — Inventory next quality-gate product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-51", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-03", plan, StringComparison.Ordinal);
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
