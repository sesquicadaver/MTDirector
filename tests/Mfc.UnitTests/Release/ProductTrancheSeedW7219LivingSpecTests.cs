using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-219: known-limitations / queue seed locks AUDIT-AN-02 (W7-220) after AUDIT-AN-01.</summary>
public sealed class ProductTrancheSeedW7219LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditAn02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-219 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-220", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-220", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-02 — Analysis fingerprint CAS must use controller-computed value", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-220", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-219 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-02", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-220", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-219", roadmap, StringComparison.Ordinal);
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
