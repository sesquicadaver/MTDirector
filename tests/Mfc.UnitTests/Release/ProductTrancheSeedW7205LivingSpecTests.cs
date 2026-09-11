using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-205: known-limitations / queue seed locks PLAN-26 inventory (W7-206) after PLAN-25.</summary>
public sealed class ProductTrancheSeedW7205LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan26AfterPlan25()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string audit = File.ReadAllText(Path.Combine(root, "docs/audits/MTDirector-audit-11cb746-20260911.md"));

        Assert.Contains("Intentional residual (W7-205 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-26", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-206", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-205", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-206", roadmap, StringComparison.Ordinal);
        Assert.Contains("PLAN-26 — Inventory code-audit remediation tranche (11cb746)", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-205", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-26", plan, StringComparison.Ordinal);
        Assert.Contains("W7-205 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-206", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RULE-01", plan26, StringComparison.Ordinal);
        Assert.Contains("11cb746", plan26, StringComparison.Ordinal);
        Assert.Contains("11cb746de60191e6eb83e52013f7f544306d5c9d", audit, StringComparison.Ordinal);
        Assert.Contains("W7-204", roadmap, StringComparison.Ordinal);
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
