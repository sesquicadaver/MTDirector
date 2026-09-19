using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-244: PLAN-28 inventory documents ranked DESK-A11Y-FIELD/CTRL rows and seeds DESK-A11Y-FIELD-01.</summary>
public sealed class Plan28DesktopResidualFieldControlAutomationW7244LivingSpecTests
{
    [Fact]
    public void Ac1Plan28InventoryDocumentsRankedRowsAndSeedsDeskA11yField01()
    {
        string root = RepoRoot();
        string plan28 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-28-desktop-residual-field-control-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string main = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-28 — Desktop residual field / control AutomationProperties tranche", plan28, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan28, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-FIELD-01", plan28, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CTRL-01", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-246", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-247", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-245", plan28, StringComparison.Ordinal);
        Assert.Contains("Zones.NewZoneKey", plan28, StringComparison.Ordinal);
        Assert.Contains("draft name (CompanyBaseline)", plan28, StringComparison.Ordinal);
        Assert.Contains("Configuration only", plan28, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan28, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-391 (#1187)", plan28, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-244 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-FIELD-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-246", limitations, StringComparison.Ordinal);

        Assert.Contains("DESK-A11Y-FIELD-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-246", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-244 | [#895](https://github.com/sesquicadaver/MTDirector/issues/895) | PLAN-28 — Inventory Desktop residual field/control AutomationProperties tranche after PLAN-27 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-246 | [#898](https://github.com/sesquicadaver/MTDirector/issues/898) | DESK-A11Y-FIELD-01 — Zones / Policies draft TextBox AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-247 | [#899](https://github.com/sesquicadaver/MTDirector/issues/899) | Seed next PLAN-28 row after DESK-A11Y-FIELD-01 → DESK-A11Y-CTRL-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-391 (#1187)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-245", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-246", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-28", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-28-desktop-residual-field-control-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-28-desktop-residual-field-control-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan28DesktopResidualFieldControlAutomationW7244", testing, StringComparison.Ordinal);

        Assert.Contains("Text=\"{Binding Zones.NewZoneKey}\"", main, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Zones.EditZoneDescription}\"", main, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Policies.DraftNameText}\"", main, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"Captures\"", main, StringComparison.Ordinal);
        Assert.Contains("Content=\"Configuration only\"", main, StringComparison.Ordinal);
        Assert.Contains("Header=\"Semantic diff\"", main, StringComparison.Ordinal);
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
