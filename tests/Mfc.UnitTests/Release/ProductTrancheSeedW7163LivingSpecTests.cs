using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-163: known-limitations / queue seed locks next PLAN-17 inventory (W7-164).</summary>
public sealed class ProductTrancheSeedW7163LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan17InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan17 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-17-desktop-incident-bind-action-automation.md"));

        Assert.Contains("Intentional residual (W7-163 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-17", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-164", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-164", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-17 — Inventory Desktop Incident bind-action AutomationProperties Living Spec product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-164", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-17", plan, StringComparison.Ordinal);
        Assert.Contains("W7-163 DONE", plan17, StringComparison.Ordinal);
        Assert.Contains("W7-164", plan17, StringComparison.Ordinal);
        Assert.Contains("W7-163", roadmap, StringComparison.Ordinal);
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
