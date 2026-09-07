using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-71: known-limitations / queue seed locks next product tranche (W7-72 PLAN-06).</summary>
public sealed class ProductTrancheSeedW771LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan06AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan05 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-05-desktop-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-71 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-06", limitations, StringComparison.Ordinal);
        Assert.Contains("Incident Desktop", limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W7-72", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-06 — Inventory next Incident Desktop operator-surface", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-72", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-06", plan, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan05, StringComparison.Ordinal);
        Assert.Contains("PLAN-06", plan05, StringComparison.Ordinal);
        Assert.Contains("W7-72", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-01", roadmap, StringComparison.Ordinal);
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
