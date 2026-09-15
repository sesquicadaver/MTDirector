using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-RO-01 / W7-266: Drift SemanticDiff + Audit PayloadJson read-only TextBoxes expose AutomationProperties.Name.</summary>
public sealed class DesktopDriftAuditReadOnlyAutomationLivingSpecTests
{
    private static readonly (string Binding, string Name)[] Fields =
    [
        ("Drift.SemanticDiffText", "Semantic diff"),
        ("Audit.SelectedEvent.PayloadJson", "Payload (JSON)"),
    ];

    [Fact]
    public void Ac1DriftAndAuditReadOnlyTextBoxesExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        foreach ((string binding, string name) in Fields)
        {
            string attrs = ExtractReadOnlyTextBoxAttrs(main, binding);
            Assert.Contains($"AutomationProperties.Name=\"{name}\"", attrs, StringComparison.Ordinal);
            Assert.Contains("IsReadOnly=\"True\"", attrs, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2Plan31AndTestingDocLockA11yRo01()
    {
        string root = FindRepoRoot();
        string plan31 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-31-desktop-residual-listbox-readonly-a11y.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-RO-01", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-266", plan31, StringComparison.Ordinal);
        Assert.Contains("DesktopDriftAuditReadOnlyAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-266 Living Spec lock)", limitations, StringComparison.Ordinal);
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
            if (attrs.Contains($"Text=\"{{Binding {bindingFragment}}}\"", StringComparison.Ordinal)
                && attrs.Contains("IsReadOnly=\"True\"", StringComparison.Ordinal))
            {
                return attrs;
            }

            idx = axaml.IndexOf("<TextBox", idx + 1, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"Read-only TextBox not found for {bindingFragment}");
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
