using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-65: known-limitations / queue seed locks next product tranche (W7-66 PLAN-05).</summary>
public sealed class ProductTrancheSeedW765LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan05AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan04 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-04-contract-tests.md"));

        Assert.Contains("Intentional residual (W7-65 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-05", limitations, StringComparison.Ordinal);
        Assert.Contains("Desktop operator-surface", limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-66", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-05 — Inventory next Desktop operator-surface", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-66", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-05", plan, StringComparison.Ordinal);
        Assert.Contains("W7-64 DONE", plan04, StringComparison.Ordinal);
        Assert.Contains("PLAN-05", plan, StringComparison.Ordinal);
        Assert.Contains("W7-66", roadmap, StringComparison.Ordinal);
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
