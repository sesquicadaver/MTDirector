using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-95: known-limitations / queue seed locks next PLAN-08 row (W7-96 DESK-POLICY-02).</summary>
public sealed class ProductTrancheSeedW795LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskPolicy02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-95 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-POLICY-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-96", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-96", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-POLICY-02 — Desktop Policy safety analysis Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-96", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-POLICY-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-94 DONE", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-POLICY-02", plan08, StringComparison.Ordinal);
        Assert.Contains("W7-96", plan08, StringComparison.Ordinal);
        Assert.Contains("W7-95", roadmap, StringComparison.Ordinal);
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
