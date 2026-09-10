using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-183: known-limitations / queue seed locks next PLAN-21 inventory (W7-184).</summary>
public sealed class ProductTrancheSeedW7183LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan21InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan21 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-21-desktop-policies-authoring-residual-automation.md"));

        Assert.Contains("Intentional residual (W7-183 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-21", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-184", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-184", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-21 — Inventory next Desktop Living Spec product tranche after PLAN-20", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-184", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-21", plan, StringComparison.Ordinal);
        Assert.Contains("W7-183 DONE", plan21, StringComparison.Ordinal);
        Assert.Contains("W7-184", plan21, StringComparison.Ordinal);
        Assert.Contains("W7-183", roadmap, StringComparison.Ordinal);
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
