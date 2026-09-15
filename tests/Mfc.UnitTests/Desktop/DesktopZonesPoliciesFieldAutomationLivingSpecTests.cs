using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-FIELD-01 / W7-246: Zones / Policies draft TextBoxes expose AutomationProperties.Name.</summary>
public sealed class DesktopZonesPoliciesFieldAutomationLivingSpecTests
{
    private static readonly (string Binding, string Name)[] Fields =
    [
        ("Zones.NewZoneKey", "key"),
        ("Zones.NewZoneName", "name"),
        ("Zones.NewZoneDescription", "description (optional)"),
        ("Zones.EditZoneName", "name"),
        ("Zones.EditZoneDescription", "description (empty clears)"),
        ("Zones.BindingValuesText", "values (comma-separated)"),
        ("Policies.RevisionIdText", "policy revision (from catalog)"),
        ("Policies.DraftNameText", "draft name (CompanyBaseline)"),
        ("Policies.SafetyDeviceIdText", "device (from inventory tree)"),
        ("Policies.ControllerSourcePrefixesText", "controller source CIDR (required)"),
        ("Policies.RuleDescriptionText", "description"),
        ("Policies.AddressNameText", "name"),
        ("Policies.AddressEntriesText", "host / CIDR / range per line"),
        ("Policies.ServiceNameText", "name"),
        ("Policies.ServiceTcpPortText", "TCP port"),
        ("Policies.ContractDisposition", "DROP / REJECT / RETURN_TO_UNMANAGED"),
        ("Policies.ComposeNodeIdText", "node (from inventory tree)"),
        ("Policies.DiffBaselineRevisionIdText", "baseline revision UUID"),
        ("Policies.CompileCapabilityHashText", "capability hash (64 hex, from Snapshots)"),
    ];

    [Fact]
    public void Ac1ZonesAndPoliciesDraftTextBoxesExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        foreach ((string binding, string name) in Fields)
        {
            string needle = $"Text=\"{{Binding {binding}}}\"";
            int index = main.IndexOf(needle, StringComparison.Ordinal);
            Assert.True(index >= 0, binding);
            string before = main.Substring(Math.Max(0, index - 280), Math.Min(280, index));
            Assert.Contains($"AutomationProperties.Name=\"{name}\"", before, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2Plan28AndTestingDocLockA11yField01()
    {
        string root = FindRepoRoot();
        string plan28 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-28-desktop-residual-field-control-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-FIELD-01", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-246 DONE", plan28, StringComparison.Ordinal);
        Assert.Contains("DesktopZonesPoliciesFieldAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-246 Living Spec lock)", limitations, StringComparison.Ordinal);
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
