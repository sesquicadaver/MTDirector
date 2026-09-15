using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-TREE-01 / W7-276: Inventory TreeView host exposes AutomationProperties.Name.</summary>
public sealed class DesktopInventoryTreeViewAutomationLivingSpecTests
{
    [Fact]
    public void Ac1InventoryTreeViewExposesAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        string attrs = ExtractTreeViewHostAttrs(main, "ItemsSource=\"{Binding Inventory.Roots}\"");
        Assert.Contains("AutomationProperties.Name=\"Inventory\"", attrs, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2Plan33AndTestingDocLockA11yTree01()
    {
        string root = FindRepoRoot();
        string plan33 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-33-desktop-inventory-treeview-a11y.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-TREE-01", plan33, StringComparison.Ordinal);
        Assert.Contains("W7-276", plan33, StringComparison.Ordinal);
        Assert.Contains("DesktopInventoryTreeViewAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-276 Living Spec lock)", limitations, StringComparison.Ordinal);
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
            if (attrs.Contains(itemsSourceFragment, StringComparison.Ordinal))
            {
                return attrs;
            }

            idx = axaml.IndexOf("<TreeView", idx + 1, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"TreeView host not found for {itemsSourceFragment}");
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string FindRepoRoot()
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
