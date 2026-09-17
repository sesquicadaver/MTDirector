using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-262: PLAN-31 inventory documents ranked DESK-A11Y-LIST/RO rows; historical: LIST-01 + RO-01 DONE; PLAN-31 COMPLETE; PLAN-32 inventory DONE; NEXT advanced to W7-269.</summary>
public sealed class Plan31DesktopResidualListboxReadonlyA11yW7262LivingSpecTests
{
    [Fact]
    public void Ac1Plan31InventoryDocumentsRankedRowsAndSeedsDeskA11yList01()
    {
        string root = RepoRoot();
        string plan31 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-31-desktop-residual-listbox-readonly-a11y.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string mainWindow = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-31 — Desktop residual ListBox / Drift–Audit read-only a11y", plan31, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan31, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-LIST-01", plan31, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-RO-01", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-264", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-265", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-263", plan31, StringComparison.Ordinal);
        Assert.Contains("184bb85", plan31, StringComparison.Ordinal);
        Assert.Contains("**43** hosts", plan31, StringComparison.Ordinal);
        Assert.Contains("Drift.SemanticDiffText", plan31, StringComparison.Ordinal);
        Assert.Contains("Audit.SelectedEvent.PayloadJson", plan31, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-336 (#1079)", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-263 (#932) DONE", plan31, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-262 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-LIST-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-264", limitations, StringComparison.Ordinal);

        Assert.Contains("DESK-A11Y-LIST-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-264", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-262 | [#931](https://github.com/sesquicadaver/MTDirector/issues/931) | PLAN-31 — Inventory Desktop residual ListBox / Drift–Audit read-only a11y (PLAN-28 deferred) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-263 | [#932](https://github.com/sesquicadaver/MTDirector/issues/932) | Seed first PLAN-31 atomic row after inventory → DESK-A11Y-LIST-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-264 | [#934](https://github.com/sesquicadaver/MTDirector/issues/934) | DESK-A11Y-LIST-01 — ListBox host AutomationProperties.Name across operator browse/select surfaces | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-265 | [#935](https://github.com/sesquicadaver/MTDirector/issues/935) | Seed next PLAN-31 row after DESK-A11Y-LIST-01 → DESK-A11Y-RO-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-266 | [#939](https://github.com/sesquicadaver/MTDirector/issues/939) | DESK-A11Y-RO-01 — Drift SemanticDiff + Audit PayloadJson read-only TextBox AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-267 | [#940](https://github.com/sesquicadaver/MTDirector/issues/940) | Seed next after DESK-A11Y-RO-01 (PLAN-31 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-268 | [#943](https://github.com/sesquicadaver/MTDirector/issues/943) | PLAN-32 — Inventory Controller host-process packaging templates (systemd / Windows Service) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-336 (#1079)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-263", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-264", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-31", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-31-desktop-residual-listbox-readonly-a11y.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-31-desktop-residual-listbox-readonly-a11y.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan31DesktopResidualListboxReadonlyA11yW7262", testing, StringComparison.Ordinal);

        Assert.Contains("ItemsSource=\"{Binding Modules}\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Zones.Zones}\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Policies.Catalog}\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Drift.Events}\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Audit.Events}\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Drift.SemanticDiffText", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Audit.SelectedEvent.PayloadJson", mainWindow, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Modules\"", ExtractListBoxHostAttrs(mainWindow, "ItemsSource=\"{Binding Modules}\""), StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Semantic diff\"", ExtractReadOnlyTextBoxAttrs(mainWindow, "Drift.SemanticDiffText"), StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Payload (JSON)\"", ExtractReadOnlyTextBoxAttrs(mainWindow, "Audit.SelectedEvent.PayloadJson"), StringComparison.Ordinal);
    }

    private static string ExtractListBoxHostAttrs(string axaml, string itemsSourceFragment)
    {
        int idx = axaml.IndexOf("<ListBox", StringComparison.Ordinal);
        while (idx >= 0)
        {
            int end = axaml.IndexOf('>', idx);
            if (end < 0)
            {
                break;
            }

            string attrs = axaml.Substring(idx, end - idx + 1);
            if (attrs.Contains(itemsSourceFragment, StringComparison.Ordinal))
            {
                return attrs;
            }

            idx = axaml.IndexOf("<ListBox", idx + 1, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"ListBox host not found for {itemsSourceFragment}");
    }

    private static string ExtractReadOnlyTextBoxAttrs(string axaml, string bindingFragment)
    {
        int idx = axaml.IndexOf("<TextBox", StringComparison.Ordinal);
        while (idx >= 0)
        {
            int end = axaml.IndexOf('>', idx);
            if (end < 0)
            {
                break;
            }

            string attrs = axaml.Substring(idx, end - idx + 1);
            if (attrs.Contains(bindingFragment, StringComparison.Ordinal) && attrs.Contains("IsReadOnly=\"True\"", StringComparison.Ordinal))
            {
                return attrs;
            }

            idx = axaml.IndexOf("<TextBox", idx + 1, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"Read-only TextBox not found for {bindingFragment}");
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
