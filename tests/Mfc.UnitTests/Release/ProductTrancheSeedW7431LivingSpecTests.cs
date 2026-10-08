using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-431: after OWN-HB-01, EVID-LIVE-01 was seeded as §3.C NEXT (W7-432).
/// </summary>
public sealed class ProductTrancheSeedW7431LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedEvidLive01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan63 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-431 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-432", limitations, StringComparison.Ordinal);
        Assert.Contains("EVID-LIVE-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-431 | [#1256](https://github.com/sesquicadaver/MTDirector/issues/1256) | Seed next after OWN-HB-01 → EVID-LIVE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-432 | [#1257](https://github.com/sesquicadaver/MTDirector/issues/1257) | EVID-LIVE-01 — Standalone live Recheck preconditions (F02 residual) | **OPEN (NEXT)**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-432 (#1257)", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **7** |", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-431 (#1256) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-432 (#1257)", plan, StringComparison.Ordinal);
        Assert.Contains("EVID-LIVE-01", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-431 (#1256) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-432 (#1257)", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-432 (#1257)", readme, StringComparison.Ordinal);
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
