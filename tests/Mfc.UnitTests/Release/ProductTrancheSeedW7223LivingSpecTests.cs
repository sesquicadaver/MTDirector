using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-223: known-limitations / queue seed locks AUDIT-GUARD-01 (W7-224) after AUDIT-DIFF-01.</summary>
public sealed class ProductTrancheSeedW7223LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditGuard01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-223 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUARD-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-224", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-224", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUARD-01 — ManagementPath must enforce complete guard contract", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-224", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUARD-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-223 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUARD-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-224", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-223", roadmap, StringComparison.Ordinal);
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
