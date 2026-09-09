using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-168: known-limitations / queue seed locks next PLAN-18 inventory (W7-169).</summary>
public sealed class ProductTrancheSeedW7168LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan18InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan18 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-18-desktop-incident-ingest-action-automation.md"));

        Assert.Contains("Intentional residual (W7-168 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-18", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-169", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-169", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-18 — Inventory Desktop Incident ingest-action AutomationProperties Living Spec product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-169", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-18", plan, StringComparison.Ordinal);
        Assert.Contains("W7-168 DONE", plan18, StringComparison.Ordinal);
        Assert.Contains("W7-169", plan18, StringComparison.Ordinal);
        Assert.Contains("W7-168", roadmap, StringComparison.Ordinal);
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
