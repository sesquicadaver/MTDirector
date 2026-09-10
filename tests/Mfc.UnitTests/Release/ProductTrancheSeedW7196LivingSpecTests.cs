using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-196: known-limitations / queue seed locks next PLAN-23 row (W7-197 DESK-A11Y-POLICY-OBJ-02).</summary>
public sealed class ProductTrancheSeedW7196LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yPolicyObj02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan23 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-23-desktop-policies-catalog-object-automation.md"));

        Assert.Contains("Intentional residual (W7-196 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-OBJ-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-197", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-197", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-OBJ-02 — Policies catalog/object Names + ack/record + authoring residual + lifecycle + shell/Incident Names regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-197", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-OBJ-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-196 DONE", plan23, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-OBJ-02", plan23, StringComparison.Ordinal);
        Assert.Contains("W7-197", plan23, StringComparison.Ordinal);
        Assert.Contains("W7-196", roadmap, StringComparison.Ordinal);
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
