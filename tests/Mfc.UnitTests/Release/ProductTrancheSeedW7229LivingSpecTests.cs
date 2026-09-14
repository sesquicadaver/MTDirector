using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-229: known-limitations / queue seed locks AUDIT-DEP-03 (W7-230) after AUDIT-DEP-02.</summary>
public sealed class ProductTrancheSeedW7229LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditDep03AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-229 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-03", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-230", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-230", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-03 — Fake VRRP reachability/traffic facts", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-230", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-03", plan, StringComparison.Ordinal);
        Assert.Contains("W7-229 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-03", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-230", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-229", roadmap, StringComparison.Ordinal);
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
