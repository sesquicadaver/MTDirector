using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-217: known-limitations / queue seed locks AUDIT-AN-01 (W7-218) after AUDIT-CAP-02.</summary>
public sealed class ProductTrancheSeedW7217LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditAn01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-217 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-218", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-218", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-01 — Validate/Record must require full analysis + mandatory tests", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-218", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-217 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-218", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-217", roadmap, StringComparison.Ordinal);
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
