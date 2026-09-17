using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-274: PLAN-33 inventory documents sole DESK-A11Y-TREE-01 row (TAB-01 dropped) and seeds TREE-01 + COMPLETE.</summary>
public sealed class Plan33DesktopInventoryTreeviewA11yW7274LivingSpecTests
{
    [Fact]
    public void Ac1Plan33InventoryDocumentsRankedTree01AndDropsTab01()
    {
        string root = RepoRoot();
        string plan33 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-33-desktop-inventory-treeview-a11y.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string mainWindow = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-33 — Desktop Inventory TreeView / residual TabControl a11y", plan33, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan33, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-TREE-01", plan33, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-TAB-01 dropped", plan33, StringComparison.Ordinal);
        Assert.Contains("W7-276", plan33, StringComparison.Ordinal);
        Assert.Contains("W7-277", plan33, StringComparison.Ordinal);
        Assert.Contains("W7-275", plan33, StringComparison.Ordinal);
        Assert.Contains("50f1ae1", plan33, StringComparison.Ordinal);
        Assert.Contains("Inventory.Roots", plan33, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-301 (#1008)", plan33, StringComparison.Ordinal);
        Assert.Contains("3** unnamed containers", plan33, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-274 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-TREE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-276", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-TAB-01 dropped", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-274 | [#955](https://github.com/sesquicadaver/MTDirector/issues/955) | PLAN-33 — Inventory Desktop Inventory TreeView / residual TabControl a11y | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-275 | [#956](https://github.com/sesquicadaver/MTDirector/issues/956) | Seed first PLAN-33 atomic row after inventory → DESK-A11Y-TREE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-276 | [#958](https://github.com/sesquicadaver/MTDirector/issues/958) | DESK-A11Y-TREE-01 — Inventory TreeView AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-277 | [#959](https://github.com/sesquicadaver/MTDirector/issues/959) | Seed next after DESK-A11Y-TREE-01 (PLAN-33 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-301 (#1008)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-275", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-276", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-33", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-33-desktop-inventory-treeview-a11y.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-33-desktop-inventory-treeview-a11y.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan33DesktopInventoryTreeviewA11yW7274", testing, StringComparison.Ordinal);

        // Evidence lock: Inventory TreeView exposes AutomationProperties.Name (TREE-01 DONE)
        const string rootsBinding = "ItemsSource=\"{Binding Inventory.Roots}\"";
        Assert.Contains(rootsBinding, mainWindow, StringComparison.Ordinal);
        string treeAttrs = ExtractTreeViewHostAttrs(mainWindow, rootsBinding);
        Assert.Contains("AutomationProperties.Name=\"Inventory\"", treeAttrs, StringComparison.Ordinal);
    }

    private static string ExtractTreeViewHostAttrs(string axaml, string itemsSourceFragment)
    {
        int idx = axaml.IndexOf("<TreeView", StringComparison.Ordinal);
        while (idx >= 0)
        {
            int end = axaml.IndexOf('>', idx);
            if (end < 0)
            {
                break;
            }

            string attrs = axaml.Substring(idx, end - idx + 1);
            if (attrs.Contains(itemsSourceFragment, StringComparison.Ordinal) && !attrs.Contains("<TreeView.ItemTemplate", StringComparison.Ordinal))
            {
                return attrs;
            }

            idx = axaml.IndexOf("<TreeView", idx + 1, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"TreeView host not found for {itemsSourceFragment}");
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
