using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-414: after AUDIT-CAP-04, §3.C NEXT is AUDIT-AN-03 (W7-415).
/// </summary>
public sealed class ProductTrancheSeedW7414LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditAn03AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-414 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-415", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-03", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-414 | [#1228](https://github.com/sesquicadaver/MTDirector/issues/1228) | Seed next after AUDIT-CAP-04 → AUDIT-AN-03 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-415 | [#1229](https://github.com/sesquicadaver/MTDirector/issues/1229) | AUDIT-AN-03 — Server-owned analysis | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-414 (#1228) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-415 (#1229)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-03", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-414 (#1228) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-415 (#1229) DONE", plan62, StringComparison.Ordinal);
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
