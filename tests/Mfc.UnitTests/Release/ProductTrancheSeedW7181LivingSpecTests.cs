using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-181: known-limitations / queue seed locks next PLAN-20 row (W7-182 DESK-A11Y-POLICY-02).</summary>
public sealed class ProductTrancheSeedW7181LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yPolicy02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan20 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-20-desktop-policies-lifecycle-action-automation.md"));

        Assert.Contains("Intentional residual (W7-181 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-182", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-182", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-02 — Policies lifecycle Names + shell/Incident Names regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-182", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-181 DONE", plan20, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-02", plan20, StringComparison.Ordinal);
        Assert.Contains("W7-182", plan20, StringComparison.Ordinal);
        Assert.Contains("W7-181", roadmap, StringComparison.Ordinal);
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
