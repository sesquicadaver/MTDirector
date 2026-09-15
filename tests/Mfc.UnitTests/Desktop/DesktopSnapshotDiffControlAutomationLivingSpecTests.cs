using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-CTRL-01 / W7-248: Snapshot/Diff ComboBox &amp; CheckBox + TabItem (+ Zones/Policies selectors) expose AutomationProperties.Name.</summary>
public sealed class DesktopSnapshotDiffControlAutomationLivingSpecTests
{
    private static readonly (string Anchor, string Name)[] Controls =
    [
        ("SelectedItem=\"{Binding Zones.SelectedBindingKind, Mode=TwoWay}\"", "binding kind"),
        ("IsChecked=\"{Binding Snapshot.ShowTechnicalView}\"", "Technical"),
        ("SelectedItem=\"{Binding Snapshot.SelectedCapture, Mode=TwoWay}\"", "Captures"),
        ("SelectedItem=\"{Binding Diff.BaseCapture, Mode=TwoWay}\"", "Base capture"),
        ("SelectedItem=\"{Binding Diff.TargetCapture, Mode=TwoWay}\"", "Target capture"),
        ("IsChecked=\"{Binding Diff.ShowConfigurationOnly}\"", "Configuration only"),
        ("IsChecked=\"{Binding Diff.ShowObservationOnly}\"", "Observations only"),
        ("SelectedItem=\"{Binding Policies.SelectedFamily, Mode=TwoWay}\"", "Families"),
        ("SelectedItem=\"{Binding Policies.SelectedChain, Mode=TwoWay}\"", "Chains"),
        ("SelectedItem=\"{Binding Policies.SelectedStage, Mode=TwoWay}\"", "Stages"),
        ("SelectedItem=\"{Binding Policies.SelectedEffect, Mode=TwoWay}\"", "Effects"),
        ("SelectedItem=\"{Binding Policies.AddressFamily, Mode=TwoWay}\"", "address family"),
        ("SelectedItem=\"{Binding Policies.ContractFamily, Mode=TwoWay}\"", "contract family"),
        ("SelectedItem=\"{Binding Policies.ContractChain, Mode=TwoWay}\"", "contract chain"),
        ("SelectedItem=\"{Binding Policies.ContractRejectMode, Mode=TwoWay}\"", "reject mode"),
        ("SelectedItem=\"{Binding Policies.DiffBaselineCatalogItem}\"", "diff baseline catalog"),
    ];

    private static readonly (string Header, string Name)[] TabItems =
    [
        ("Snapshot", "Snapshot"),
        ("Configuration", "Configuration"),
        ("Observations", "Observations"),
        ("Semantic diff", "Semantic diff"),
        ("Onboarding", "Onboarding"),
        ("Deploy", "Deploy"),
        ("Incident", "Incident"),
    ];

    [Fact]
    public void Ac1SnapshotDiffZonesPoliciesControlsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        foreach ((string anchor, string name) in Controls)
        {
            int index = main.IndexOf(anchor, StringComparison.Ordinal);
            Assert.True(index >= 0, anchor);
            string before = main.Substring(Math.Max(0, index - 280), Math.Min(280, index));
            Assert.Contains($"AutomationProperties.Name=\"{name}\"", before, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2PanelTabItemsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        foreach ((string header, string name) in TabItems)
        {
            string needle = $"Header=\"{header}\"";
            int index = main.IndexOf(needle, StringComparison.Ordinal);
            Assert.True(index >= 0, header);
            string window = main.Substring(index, Math.Min(120, main.Length - index));
            Assert.Contains($"AutomationProperties.Name=\"{name}\"", window, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac3Plan28AndTestingDocLockA11yCtrl01()
    {
        string root = FindRepoRoot();
        string plan28 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-28-desktop-residual-field-control-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-CTRL-01", plan28, StringComparison.Ordinal);
        Assert.Contains("W7-248 DONE", plan28, StringComparison.Ordinal);
        Assert.Contains("DesktopSnapshotDiffControlAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-248 Living Spec lock)", limitations, StringComparison.Ordinal);
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
