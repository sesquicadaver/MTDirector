using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-178: known-limitations / queue seed locks next PLAN-20 inventory (W7-179).</summary>
public sealed class ProductTrancheSeedW7178LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan20InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan20 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-20-desktop-policies-lifecycle-action-automation.md"));

        Assert.Contains("Intentional residual (W7-178 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-20", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-179", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-179", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-20 — Inventory Desktop Policies lifecycle-action AutomationProperties Living Spec product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-179", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-20", plan, StringComparison.Ordinal);
        Assert.Contains("W7-178 DONE", plan20, StringComparison.Ordinal);
        Assert.Contains("W7-179", plan20, StringComparison.Ordinal);
        Assert.Contains("W7-178", roadmap, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-179 (#760)", roadmap, StringComparison.Ordinal);
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
