using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-232: historical residual lock AUDIT-GUI-01 DONE (not current §3.C NEXT).</summary>
public sealed class ProductTrancheAuditGui01W7232LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueLockAuditGui01DoneHistoricalResidual()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-232 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-01 closed", limitations, StringComparison.Ordinal);
        Assert.Contains("CreatePlanFromSealedArtifacts", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-233", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AUTH-01", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-232 | [#871](https://github.com/sesquicadaver/MTDirector/issues/871) | AUDIT-GUI-01 — Onboarding/Deployment synthetic payloads; Deploy never enables | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("W7-232", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-232 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-233", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-233 DONE", plan26, StringComparison.Ordinal);
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
