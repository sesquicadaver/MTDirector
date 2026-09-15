using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-231: known-limitations / queue seed locks AUDIT-GUI-01 (W7-232) after AUDIT-DEP-03.</summary>
public sealed class ProductTrancheSeedW7231LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditGui01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-231 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-232", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-232", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-01 — Onboarding/Deployment synthetic payloads; Deploy never enables", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-232", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-231 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-232", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-231", roadmap, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-232**", limitations, StringComparison.Ordinal);
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
