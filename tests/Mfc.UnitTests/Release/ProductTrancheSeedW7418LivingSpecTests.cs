using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-418: after AUDIT-BIND-01, §3.C NEXT is AUDIT-GUI-02 (W7-419).
/// </summary>
public sealed class ProductTrancheSeedW7418LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditGui02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-418 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-419", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-02", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-418 | [#1235](https://github.com/sesquicadaver/MTDirector/issues/1235) | Seed next after AUDIT-BIND-01 → AUDIT-GUI-02 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-419 | [#1236](https://github.com/sesquicadaver/MTDirector/issues/1236) | AUDIT-GUI-02 — Controller onboarding + stale policy GUI | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-418 (#1235) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-419 (#1236)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-02", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-418 (#1235) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-419 (#1236) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", readme, StringComparison.Ordinal);
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
