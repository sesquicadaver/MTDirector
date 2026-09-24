using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-392: freeze closed the correlation-id / fault-text wave.
/// PLAN-62 may exist when seeded from audit TOR (not ErrorText grepping).
/// </summary>
public sealed class ProductTrancheSeedW7392LivingSpecTests
{
    [Fact]
    public void Ac1FreezeClosedCorrelationWaveAndAllowsAuditTorPlan62()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan61 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-61-desktop-connection-disconnected-fault-text.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));

        Assert.Contains("Intentional residual (W7-392 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-392 (#1191) DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("correlation-id / fault-text", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-62", limitations, StringComparison.Ordinal);
        Assert.Contains("acd0759", limitations, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-392 | [#1191](https://github.com/sesquicadaver/MTDirector/issues/1191) | Freeze — no further correlation-id / fault-text plans without a pre-existing TOR | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-393 | [#1195](https://github.com/sesquicadaver/MTDirector/issues/1195) | PLAN-62 — Inventory repository-audit remediation (acd0759) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", roadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("§3.C NEXT = W7-392 (#1191)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-392 (#1191) DONE", plan61, StringComparison.Ordinal);
        Assert.Contains("PLAN-62", plan61, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", plan61, StringComparison.Ordinal);

        Assert.Contains("Freeze **W7-392 (#1191) DONE**", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-62", plan, StringComparison.Ordinal);
        Assert.Contains("plan-62-audit-remediation-acd0759.md", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", plan, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "docs/planning/plan-62-desktop-correlation-id.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md")));
        Assert.Contains("AUDIT-STATUS-01", plan62, StringComparison.Ordinal);
        Assert.Contains("acd0759e85414a83460c4cab971db2b0b58b30cd", plan62, StringComparison.Ordinal);
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
