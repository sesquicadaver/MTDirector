using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-240: DESK-A11Y-SNAP-01 DONE; historical seed lock — W7-242 PANEL-01 DONE advanced NEXT to W7-243.
/// </summary>
public sealed class ProductTrancheSeedW7240LivingSpecTests
{
    [Fact]
    public void Ac1Snap01DoneAndQueueSeedsDeskA11yPanel01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan27 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-27-desktop-snapshot-panel-automation.md"));

        Assert.Contains("Intentional residual (W7-240 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-SNAP-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-241", limitations, StringComparison.Ordinal);
        Assert.Contains("DesktopSnapshotAutomationLivingSpecTests", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-240 | [#886](https://github.com/sesquicadaver/MTDirector/issues/886) | DESK-A11Y-SNAP-01 — Snapshot Capture/Reload/Compare/Copy AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-241 | [#887](https://github.com/sesquicadaver/MTDirector/issues/887) | Seed next PLAN-27 row after DESK-A11Y-SNAP-01 → DESK-A11Y-PANEL-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-352 (#1111)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-240 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-241", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-352 (#1111)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-240 DONE", plan27, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-SNAP-01", plan27, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", plan27, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-352 (#1111)", plan27, StringComparison.Ordinal);
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
