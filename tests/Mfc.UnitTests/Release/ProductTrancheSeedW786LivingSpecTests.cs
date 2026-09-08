using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-86: known-limitations / queue seed locks next PLAN-07 row (W7-87 DESK-INVENTORY-01).</summary>
public sealed class ProductTrancheSeedW786LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskInventory01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-86 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-INVENTORY-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-87", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-87", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-INVENTORY-01 — Desktop Inventory Living Spec vs InventoryGrpcHost", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-87", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-INVENTORY-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-85 DONE", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-INVENTORY-01", plan07, StringComparison.Ordinal);
        Assert.Contains("W7-87", plan07, StringComparison.Ordinal);
        Assert.Contains("W7-86", roadmap, StringComparison.Ordinal);
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
