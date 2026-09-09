using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-166: known-limitations / queue seed locks next PLAN-17 row (W7-167 DESK-A11Y-ACTION-02).</summary>
public sealed class ProductTrancheSeedW7166LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yAction02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan17 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-17-desktop-incident-bind-action-automation.md"));

        Assert.Contains("Intentional residual (W7-166 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-ACTION-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-167", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-167", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-ACTION-02 — Bind Name + Incident field Names regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-167", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-ACTION-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-166 DONE", plan17, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-ACTION-02", plan17, StringComparison.Ordinal);
        Assert.Contains("W7-167", plan17, StringComparison.Ordinal);
        Assert.Contains("W7-166", roadmap, StringComparison.Ordinal);
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
