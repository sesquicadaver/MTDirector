using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-198: known-limitations / queue seed locks next PLAN-24 inventory (W7-199).</summary>
public sealed class ProductTrancheSeedW7198LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan24InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan24 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-24-desktop-onboarding-deployment-automation.md"));

        Assert.Contains("Intentional residual (W7-198 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-24", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-199", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-199", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-24 — Inventory next Desktop Living Spec product tranche after PLAN-23", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-199", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-24", plan, StringComparison.Ordinal);
        Assert.Contains("W7-198 DONE", plan24, StringComparison.Ordinal);
        Assert.Contains("W7-199", plan24, StringComparison.Ordinal);
        Assert.Contains("W7-198", roadmap, StringComparison.Ordinal);
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
