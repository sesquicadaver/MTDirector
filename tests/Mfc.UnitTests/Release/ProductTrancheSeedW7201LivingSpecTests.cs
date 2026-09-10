using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-201: known-limitations / queue seed locks next PLAN-24 row (W7-202 DESK-A11Y-OPS-02).</summary>
public sealed class ProductTrancheSeedW7201LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yOps02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan24 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-24-desktop-onboarding-deployment-automation.md"));

        Assert.Contains("Intentional residual (W7-201 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-OPS-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-202", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-202", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-OPS-02 — Onboarding/Deployment Names + Policies/shell/Incident Names regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-202", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-OPS-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-201 DONE", plan24, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-OPS-02", plan24, StringComparison.Ordinal);
        Assert.Contains("W7-202", plan24, StringComparison.Ordinal);
        Assert.Contains("W7-201", roadmap, StringComparison.Ordinal);
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
