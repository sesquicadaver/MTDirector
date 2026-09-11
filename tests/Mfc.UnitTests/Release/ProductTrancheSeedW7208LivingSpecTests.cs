using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-208: known-limitations / queue seed locks next PLAN-25 row (W7-209 DESK-A11Y-INV-02).</summary>
public sealed class ProductTrancheSeedW7208LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yInv02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan25 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-25-desktop-inventory-zones-add-router-automation.md"));

        Assert.Contains("Intentional residual (W7-208 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INV-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-209", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-209", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INV-02 — Inventory/Zones Names + ops + Policies/shell/Incident Names regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-209", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INV-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-208 DONE", plan25, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INV-02", plan25, StringComparison.Ordinal);
        Assert.Contains("W7-209", plan25, StringComparison.Ordinal);
        Assert.Contains("W7-208", roadmap, StringComparison.Ordinal);
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
