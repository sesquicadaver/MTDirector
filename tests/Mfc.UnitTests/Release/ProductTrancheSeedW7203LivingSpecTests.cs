using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-203: known-limitations / queue seed locks next PLAN-25 inventory (W7-204).</summary>
public sealed class ProductTrancheSeedW7203LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan25InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan25 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-25-desktop-inventory-zones-add-router-automation.md"));

        Assert.Contains("Intentional residual (W7-203 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-25", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-204", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-204", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-25 — Inventory next Desktop Living Spec product tranche after PLAN-24", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-204", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-25", plan, StringComparison.Ordinal);
        Assert.Contains("W7-203 DONE", plan25, StringComparison.Ordinal);
        Assert.Contains("W7-204", plan25, StringComparison.Ordinal);
        Assert.Contains("W7-203", roadmap, StringComparison.Ordinal);
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
