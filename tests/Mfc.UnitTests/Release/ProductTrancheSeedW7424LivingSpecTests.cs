using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-424: after AUDIT-M7-01, §3.C advanced through AUDIT-ACC-01; current NEXT is PLAN62-DONE-01 (W7-427).
/// </summary>
public sealed class ProductTrancheSeedW7424LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditAcc01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-424 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-425", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-ACC-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-424 | [#1244](https://github.com/sesquicadaver/MTDirector/issues/1244) | Seed next after AUDIT-M7-01 → AUDIT-ACC-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-425 | [#1245](https://github.com/sesquicadaver/MTDirector/issues/1245) | AUDIT-ACC-01 — Acceptance by behavior not file presence | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-424 (#1244) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-425 (#1245)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-ACC-01", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-424 (#1244) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-425 (#1245) DONE", plan62, StringComparison.Ordinal);
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
