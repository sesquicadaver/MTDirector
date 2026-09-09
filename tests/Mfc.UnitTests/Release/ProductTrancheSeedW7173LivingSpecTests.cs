using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-173: known-limitations / queue seed locks next PLAN-19 inventory (W7-174).</summary>
public sealed class ProductTrancheSeedW7173LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan19InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan19 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-19-desktop-shell-connect-disconnect-automation.md"));

        Assert.Contains("Intentional residual (W7-173 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-19", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-174", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-174", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-19 — Inventory Desktop shell Connect/Disconnect AutomationProperties Living Spec product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-174", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-19", plan, StringComparison.Ordinal);
        Assert.Contains("W7-173 DONE", plan19, StringComparison.Ordinal);
        Assert.Contains("W7-174", plan19, StringComparison.Ordinal);
        Assert.Contains("W7-173", roadmap, StringComparison.Ordinal);
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
