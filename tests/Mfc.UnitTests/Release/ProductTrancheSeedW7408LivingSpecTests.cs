using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-408: after AUDIT-CLK-01, §3.C NEXT is AUDIT-RPC-01 (W7-409).
/// </summary>
public sealed class ProductTrancheSeedW7408LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditRpc01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-408 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-409", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RPC-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-408 | [#1217](https://github.com/sesquicadaver/MTDirector/issues/1217) | Seed next after AUDIT-CLK-01 → AUDIT-RPC-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-409 | [#1218](https://github.com/sesquicadaver/MTDirector/issues/1218) | AUDIT-RPC-01 — Fast Start + live Watch | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-410 (#1220)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-408 (#1217) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-409 (#1218)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RPC-01", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RPC-01 W7-409 (#1218) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-410 (#1220)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-410 (#1220)", readme, StringComparison.Ordinal);
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
