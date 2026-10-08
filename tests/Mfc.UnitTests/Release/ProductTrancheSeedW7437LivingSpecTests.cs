using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-437: after CAP-IDEM-01, PLAN63-DONE-01 was seeded; DONE-01 landed as W7-438;
/// PLAN-63 COMPLETE and §3.C NEXT = none.
/// </summary>
public sealed class ProductTrancheSeedW7437LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan63Done01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan63 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-437 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-438", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN63-DONE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-438 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-437 | [#1263](https://github.com/sesquicadaver/MTDirector/issues/1263) | Seed next after CAP-IDEM-01 → PLAN63-DONE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-438 | [#1264](https://github.com/sesquicadaver/MTDirector/issues/1264) | PLAN63-DONE-01 — PLAN-63 COMPLETE; handoff wave B Layer C | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **0** |", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-437 (#1263) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-438 (#1264)", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN63-DONE-01", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-437 (#1263) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", readme, StringComparison.Ordinal);
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
