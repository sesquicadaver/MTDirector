using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-186: known-limitations / queue seed locks next PLAN-21 row (W7-187 DESK-A11Y-POLICY-EDIT-02).</summary>
public sealed class ProductTrancheSeedW7186LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yPolicyEdit02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan21 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-21-desktop-policies-authoring-residual-automation.md"));

        Assert.Contains("Intentional residual (W7-186 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-EDIT-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-187", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-187", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-EDIT-02 — Policies authoring residual Names + lifecycle + shell/Incident Names regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-187", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-EDIT-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-186 DONE", plan21, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-EDIT-02", plan21, StringComparison.Ordinal);
        Assert.Contains("W7-187", plan21, StringComparison.Ordinal);
        Assert.Contains("W7-186", roadmap, StringComparison.Ordinal);
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
