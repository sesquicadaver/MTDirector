using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-158: known-limitations / queue seed locks next PLAN-16 inventory (W7-159).</summary>
public sealed class ProductTrancheSeedW7158LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan16InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan16 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-16-desktop-incident-automation-properties.md"));

        Assert.Contains("Intentional residual (W7-158 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-16", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-159", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-159", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-16 — Inventory Desktop Incident AutomationProperties accessible-name Living Spec product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-159", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-16", plan, StringComparison.Ordinal);
        Assert.Contains("W7-158 DONE", plan16, StringComparison.Ordinal);
        Assert.Contains("W7-159", plan16, StringComparison.Ordinal);
        Assert.Contains("W7-158", roadmap, StringComparison.Ordinal);
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
