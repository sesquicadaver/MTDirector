using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-153: known-limitations / queue seed locks next PLAN-15 inventory (W7-154).</summary>
public sealed class ProductTrancheSeedW7153LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan15InventoryAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan15 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-15-desktop-incident-mfc-field-style-hygiene.md"));

        Assert.Contains("Intentional residual (W7-153 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-15", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-154", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-154", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-15 — Inventory Desktop Incident mfc-field style hygiene Living Spec product tranche", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-154", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-15", plan, StringComparison.Ordinal);
        Assert.Contains("W7-153 DONE", plan15, StringComparison.Ordinal);
        Assert.Contains("W7-154", plan15, StringComparison.Ordinal);
        Assert.Contains("W7-153", roadmap, StringComparison.Ordinal);
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
