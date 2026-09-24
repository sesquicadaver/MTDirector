using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-404: after AUDIT-EVID-01, AUDIT-RB-01 was seeded (W7-405);
/// queue may have advanced past that row.
/// </summary>
public sealed class ProductTrancheSeedW7404LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditRb01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-404 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-405", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RB-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-404 | [#1211](https://github.com/sesquicadaver/MTDirector/issues/1211) | Seed next after AUDIT-EVID-01 → AUDIT-RB-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-405 | [#1212](https://github.com/sesquicadaver/MTDirector/issues/1212) | AUDIT-RB-01 — Unified strict rollback | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-404 (#1211) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-405 (#1212)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RB-01", plan62, StringComparison.Ordinal);
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
