using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-429: after PLAN-63 inventory, OWN-HB-01 was seeded (W7-430); queue may have advanced.
/// </summary>
public sealed class ProductTrancheSeedW7429LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedOwnHb01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan63 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-429 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-430", limitations, StringComparison.Ordinal);
        Assert.Contains("OWN-HB-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-429 | [#1254](https://github.com/sesquicadaver/MTDirector/issues/1254) | Seed first PLAN-63 atomic row after inventory → OWN-HB-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-430 | [#1255](https://github.com/sesquicadaver/MTDirector/issues/1255) | OWN-HB-01 — Onboarding lock heartbeat (F01 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-431 | [#1256](https://github.com/sesquicadaver/MTDirector/issues/1256) | Seed next after OWN-HB-01 → EVID-LIVE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-436 (#1261)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-429 (#1254) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-430 (#1255)", plan, StringComparison.Ordinal);
        Assert.Contains("OWN-HB-01", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-436 (#1261)", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-436 (#1261)", readme, StringComparison.Ordinal);
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
