using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-148: known-limitations / queue seed locks next PLAN-14 inventory (W7-149).</summary>
public sealed class ProductTrancheSeedW7148LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan14InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan14 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-14-desktop-avalonia-placeholder-incident-surface.md"));

        Assert.Contains("Intentional residual (W7-148 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-14", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-149", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-149", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-14 — Inventory Desktop Avalonia PlaceholderText / Incident surface Living Spec product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-149", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-14", plan, StringComparison.Ordinal);
        Assert.Contains("W7-148 DONE", plan14, StringComparison.Ordinal);
        Assert.Contains("W7-149", plan14, StringComparison.Ordinal);
        Assert.Contains("W7-148", roadmap, StringComparison.Ordinal);
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
