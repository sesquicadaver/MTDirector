using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-406: after AUDIT-RB-01, §3.C NEXT is AUDIT-CLK-01 (W7-407).
/// </summary>
public sealed class ProductTrancheSeedW7406LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditClk01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-406 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-407", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CLK-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-406 | [#1214](https://github.com/sesquicadaver/MTDirector/issues/1214) | Seed next after AUDIT-RB-01 → AUDIT-CLK-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-407 | [#1215](https://github.com/sesquicadaver/MTDirector/issues/1215) | AUDIT-CLK-01 — RouterOS clock / TTL budget | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-406 (#1214) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-407 (#1215)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CLK-01", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", readme, StringComparison.Ordinal);
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
