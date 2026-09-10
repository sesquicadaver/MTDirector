using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-193: known-limitations / queue seed locks next PLAN-23 inventory (W7-194).</summary>
public sealed class ProductTrancheSeedW7193LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan23InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan23 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-23-desktop-policies-catalog-object-automation.md"));

        Assert.Contains("Intentional residual (W7-193 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-23", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-194", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-194", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-23 — Inventory next Desktop Living Spec product tranche after PLAN-22", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-194", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-23", plan, StringComparison.Ordinal);
        Assert.Contains("W7-193 DONE", plan23, StringComparison.Ordinal);
        Assert.Contains("W7-194", plan23, StringComparison.Ordinal);
        Assert.Contains("W7-193", roadmap, StringComparison.Ordinal);
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
