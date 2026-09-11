using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-211: known-limitations / queue seed locks AUDIT-CTX-01 (W7-212) after AUDIT-RULE-01.</summary>
public sealed class ProductTrancheSeedW7211LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditCtx01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-211 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CTX-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-212", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-212", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CTX-01 — Node switch must invalidate mutation context", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-212", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CTX-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-211 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CTX-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-212", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-211", roadmap, StringComparison.Ordinal);
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
