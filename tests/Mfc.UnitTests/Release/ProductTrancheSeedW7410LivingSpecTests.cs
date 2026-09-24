using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-410: after AUDIT-RPC-01, §3.C NEXT is AUDIT-CAP-03 (W7-411).
/// </summary>
public sealed class ProductTrancheSeedW7410LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditCap03AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-410 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-411", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-03", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-410 | [#1220](https://github.com/sesquicadaver/MTDirector/issues/1220) | Seed next after AUDIT-RPC-01 → AUDIT-CAP-03 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-411 | [#1221](https://github.com/sesquicadaver/MTDirector/issues/1221) | AUDIT-CAP-03 — Full capture projection | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-412 | [#1224](https://github.com/sesquicadaver/MTDirector/issues/1224) | Seed next after AUDIT-CAP-03 → AUDIT-CAP-04 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-413 | [#1226](https://github.com/sesquicadaver/MTDirector/issues/1226) | AUDIT-CAP-04 — Capture attempt identity | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-410 (#1220) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-411 (#1221)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-03", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-410 (#1220) DONE", plan62, StringComparison.Ordinal);
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
