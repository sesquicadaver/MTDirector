using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-227: known-limitations / queue seed locks AUDIT-DEP-02 (W7-228) after AUDIT-DEP-01.</summary>
public sealed class ProductTrancheSeedW7227LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditDep02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-227 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-228", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-228", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-02 — Watchdog durable clock/TTL; cleanup result must not be ignored", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-228", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-227 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-02", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-228", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-227", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-228", roadmap, StringComparison.Ordinal);
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
