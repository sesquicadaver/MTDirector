using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-433: after EVID-LIVE-01, M7-PRES-01 was seeded as §3.C NEXT (W7-434).
/// </summary>
public sealed class ProductTrancheSeedW7433LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedM7Pres01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan63 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-433 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-434", limitations, StringComparison.Ordinal);
        Assert.Contains("M7-PRES-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-433 | [#1258](https://github.com/sesquicadaver/MTDirector/issues/1258) | Seed next after EVID-LIVE-01 → M7-PRES-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-434 | [#1259](https://github.com/sesquicadaver/MTDirector/issues/1259) | M7-PRES-01 — Wire OpenEndpointPresence from capture (F13 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-435 | [#1260](https://github.com/sesquicadaver/MTDirector/issues/1260) | Seed next after M7-PRES-01 → CAP-IDEM-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-438 (#1264)", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **1** |", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-433 (#1258) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-434 (#1259)", plan, StringComparison.Ordinal);
        Assert.Contains("M7-PRES-01", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-433 (#1258) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-435 (#1260) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-438 (#1264)", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-438 (#1264)", readme, StringComparison.Ordinal);
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
