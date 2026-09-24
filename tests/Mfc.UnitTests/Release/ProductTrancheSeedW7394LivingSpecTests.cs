using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-394: after PLAN-62 inventory, AUDIT-STATUS-01 was seeded (W7-395);
/// queue may have advanced past that row.
/// </summary>
public sealed class ProductTrancheSeedW7394LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditStatus01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-394 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-395", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-STATUS-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-394 | [#1196](https://github.com/sesquicadaver/MTDirector/issues/1196) | Seed first PLAN-62 atomic row after inventory → AUDIT-STATUS-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-395 | [#1197](https://github.com/sesquicadaver/MTDirector/issues/1197) | AUDIT-STATUS-01 — Honest MVP/M7/write-path status vs audit acd0759 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-413 (#1226)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-394 (#1196) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-395 (#1197)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-STATUS-01", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-413 (#1226)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-413 (#1226)", readme, StringComparison.Ordinal);
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
