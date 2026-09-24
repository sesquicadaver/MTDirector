using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-400: after AUDIT-OWN-01, AUDIT-COMMIT-01 was seeded (W7-401);
/// queue may have advanced past that row.
/// </summary>
public sealed class ProductTrancheSeedW7400LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditCommit01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-400 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-401", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-COMMIT-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-400 | [#1205](https://github.com/sesquicadaver/MTDirector/issues/1205) | Seed next after AUDIT-OWN-01 → AUDIT-COMMIT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-401 | [#1206](https://github.com/sesquicadaver/MTDirector/issues/1206) | AUDIT-COMMIT-01 — Commit snapshot + journal persist | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-411 (#1221)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-400 (#1205) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-401 (#1206)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-COMMIT-01", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-411 (#1221)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-411 (#1221)", readme, StringComparison.Ordinal);
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
