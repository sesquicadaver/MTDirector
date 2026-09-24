using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-420: after AUDIT-GUI-02, §3.C advanced through AUDIT-DRIFT-01; current NEXT is AUDIT-ACC-01 (W7-425).
/// </summary>
public sealed class ProductTrancheSeedW7420LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditDrift01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-420 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-421", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DRIFT-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-420 | [#1238](https://github.com/sesquicadaver/MTDirector/issues/1238) | Seed next after AUDIT-GUI-02 → AUDIT-DRIFT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-421 | [#1239](https://github.com/sesquicadaver/MTDirector/issues/1239) | AUDIT-DRIFT-01 — Drift from live RouterOS read | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-420 (#1238) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-421 (#1239)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DRIFT-01", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-420 (#1238) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-421 (#1239) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", readme, StringComparison.Ordinal);
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
