using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-435: after M7-PRES-01, CAP-IDEM-01 was seeded; CAP-IDEM landed as W7-436 DONE;
/// seed W7-437 advanced §3.C NEXT to PLAN63-DONE-01 (W7-438).
/// </summary>
public sealed class ProductTrancheSeedW7435LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedCapIdem01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan63 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-435 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-436", limitations, StringComparison.Ordinal);
        Assert.Contains("CAP-IDEM-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-436 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-435 | [#1260](https://github.com/sesquicadaver/MTDirector/issues/1260) | Seed next after M7-PRES-01 → CAP-IDEM-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-436 | [#1261](https://github.com/sesquicadaver/MTDirector/issues/1261) | CAP-IDEM-01 — Capture idempotency unique includes TargetId (F09 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **0** |", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-435 (#1260) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-436 (#1261) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("CAP-IDEM-01", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-435 (#1260) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-436 (#1261) DONE", plan63, StringComparison.Ordinal);
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
