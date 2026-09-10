using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-188: known-limitations / queue seed locks next PLAN-22 inventory (W7-189).</summary>
public sealed class ProductTrancheSeedW7188LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan22InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan22 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-22-desktop-policies-ack-record-automation.md"));

        Assert.Contains("Intentional residual (W7-188 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-22", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-189", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-189", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-22 — Inventory next Desktop Living Spec product tranche after PLAN-21", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-189", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-22", plan, StringComparison.Ordinal);
        Assert.Contains("W7-188 DONE", plan22, StringComparison.Ordinal);
        Assert.Contains("W7-189", plan22, StringComparison.Ordinal);
        Assert.Contains("W7-188", roadmap, StringComparison.Ordinal);
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
