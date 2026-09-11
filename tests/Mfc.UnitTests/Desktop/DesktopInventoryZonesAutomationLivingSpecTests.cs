using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-INV-01 / W7-207: Inventory/Zones primary actions expose AutomationProperties.Name.</summary>
public sealed class DesktopInventoryZonesAutomationLivingSpecTests
{
    [Fact]
    public void Ac1InventoryAndZonesButtonsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");

        // F5 KeyBinding also binds Inventory.RefreshCommand; Name is on the toolbar Button (2nd occurrence).
        AssertNameBeforeNthCommand(main, "Inventory.RefreshCommand", 1, "Refresh");
        AssertNameBeforeNthCommand(main, "AddRouter.ProbeCommand", 0, "Inventory Probe");
        AssertNameBeforeNthCommand(main, "AddRouter.ProbeCommand", 1, "Add router Probe");

        AssertNameBeforeCommand(main, "Zones.RefreshCommand", "Refresh");
        AssertNameBeforeCommand(main, "Zones.ResolveCommand", "Resolve node");
        AssertNameBeforeCommand(main, "Zones.ResolveDeviceCommand", "Resolve device");
        AssertNameBeforeCommand(main, "Zones.CreateZoneCommand", "Create");
        AssertNameBeforeCommand(main, "Zones.DeleteZoneCommand", "Delete");
        AssertNameBeforeCommand(main, "Zones.UpdateZoneCommand", "Update zone");
        AssertNameBeforeCommand(main, "Zones.UpsertBindingCommand", "Upsert binding");
        AssertNameBeforeCommand(main, "Zones.DeleteBindingCommand", "Delete binding");
    }

    [Fact]
    public void Ac2Plan25AndTestingDocLockA11yInv01()
    {
        string root = FindRepoRoot();
        string plan25 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-25-desktop-inventory-zones-add-router-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-INV-01", plan25, StringComparison.Ordinal);
        Assert.Contains("W7-207 DONE", plan25, StringComparison.Ordinal);
        Assert.Contains("DesktopInventoryZonesAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-207 Living Spec lock)", limitations, StringComparison.Ordinal);
    }

    private static void AssertNameBeforeCommand(string main, string commandBinding, string name)
    {
        AssertNameBeforeNthCommand(main, commandBinding, 0, name);
    }

    private static void AssertNameBeforeNthCommand(string main, string commandBinding, int occurrence, string name)
    {
        string needle = $"Command=\"{{Binding {commandBinding}}}\"";
        int index = -1;
        int start = 0;
        for (int i = 0; i <= occurrence; i++)
        {
            index = main.IndexOf(needle, start, StringComparison.Ordinal);
            Assert.True(index > 0, $"Missing occurrence {i} of {needle}");
            start = index + needle.Length;
        }

        string before = main.Substring(Math.Max(0, index - 280), Math.Min(280, index));
        Assert.Contains($"AutomationProperties.Name=\"{name}\"", before, StringComparison.Ordinal);
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
