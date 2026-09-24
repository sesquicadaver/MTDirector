using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-412: after AUDIT-CAP-03, §3.C NEXT advanced through AUDIT-CAP-04; current NEXT is seed → AUDIT-AN-03.
/// </summary>
public sealed class ProductTrancheSeedW7412LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditCap04AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-412 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-413", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-04", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-412 | [#1224](https://github.com/sesquicadaver/MTDirector/issues/1224) | Seed next after AUDIT-CAP-03 → AUDIT-CAP-04 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-413 | [#1226](https://github.com/sesquicadaver/MTDirector/issues/1226) | AUDIT-CAP-04 — Capture attempt identity | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-414 | [#1228](https://github.com/sesquicadaver/MTDirector/issues/1228) | Seed next after AUDIT-CAP-04 → AUDIT-AN-03 | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-412 (#1224) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-413 (#1226)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-04", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-412 (#1224) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-413 (#1226) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-414 (#1228) OPEN (NEXT)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", readme, StringComparison.Ordinal);
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
