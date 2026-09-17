using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-238: PLAN-27 inventory documents ranked DESK-A11Y-SNAP/PANEL rows and seeds DESK-A11Y-SNAP-01.</summary>
public sealed class Plan27DesktopSnapshotPanelAutomationW7238LivingSpecTests
{
    [Fact]
    public void Ac1Plan27InventoryDocumentsRankedRowsAndSeedsDeskA11ySnap01()
    {
        string root = RepoRoot();
        string plan27 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-27-desktop-snapshot-panel-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string main = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-27 — Desktop Snapshot / Node / Drift / Audit AutomationProperties residual tranche", plan27, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan27, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-SNAP-01", plan27, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-PANEL-01", plan27, StringComparison.Ordinal);
        Assert.Contains("W7-240", plan27, StringComparison.Ordinal);
        Assert.Contains("W7-241", plan27, StringComparison.Ordinal);
        Assert.Contains("W7-239", plan27, StringComparison.Ordinal);
        Assert.Contains("Copy sanitized", plan27, StringComparison.Ordinal);
        Assert.Contains("Validate (last captures)", plan27, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan27, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-325 (#1056)", plan27, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-238 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-SNAP-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-240", limitations, StringComparison.Ordinal);

        Assert.Contains("DESK-A11Y-SNAP-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-240", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-238 | [#883](https://github.com/sesquicadaver/MTDirector/issues/883) | PLAN-27 — Inventory Desktop Snapshot/Node/Drift/Audit AutomationProperties residual tranche after PLAN-26 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-325 (#1056)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-239", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-240", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-27", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-27-desktop-snapshot-panel-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-27-desktop-snapshot-panel-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan27DesktopSnapshotPanelAutomationW7238", testing, StringComparison.Ordinal);

        Assert.Contains("Content=\"Reload\"", main, StringComparison.Ordinal);
        Assert.Contains("Content=\"Capture\"", main, StringComparison.Ordinal);
        Assert.Contains("Content=\"Copy sanitized\"", main, StringComparison.Ordinal);
        Assert.Contains("Content=\"Compare\"", main, StringComparison.Ordinal);
        Assert.Contains("Content=\"Reload captures\"", main, StringComparison.Ordinal);
        Assert.Contains("Content=\"Validate (last captures)\"", main, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding Drift.RefreshCommand}\"", main, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding Audit.RefreshCommand}\"", main, StringComparison.Ordinal);
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
