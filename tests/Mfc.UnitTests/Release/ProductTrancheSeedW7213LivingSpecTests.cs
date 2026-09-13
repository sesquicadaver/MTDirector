using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-213: known-limitations / queue seed locks AUDIT-CAP-01 (W7-214) after AUDIT-CTX-01.</summary>
public sealed class ProductTrancheSeedW7213LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditCap01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-213 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-214", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-214", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-01 — Canonical filter must include firewall match fields", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-214", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-213 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-214", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-213", roadmap, StringComparison.Ordinal);
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
