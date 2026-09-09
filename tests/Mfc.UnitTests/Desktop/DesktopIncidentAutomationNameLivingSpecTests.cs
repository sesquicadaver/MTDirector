using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-01 / W7-160: Incident TextBoxes expose AutomationProperties.Name.</summary>
public sealed class DesktopIncidentAutomationNameLivingSpecTests
{
    private static readonly (string Binding, string Name)[] Fields =
    [
        ("Incident.SourceEventId", "Source event id"),
        ("Incident.Category", "Category"),
        ("Incident.DeduplicationKey", "Deduplication key"),
        ("Incident.Confidence", "Confidence (0–100)"),
        ("Incident.EndpointIdText", "Endpoint id"),
        ("Incident.PresenceIdText", "Presence id"),
        ("Incident.EnforcementNodeIdText", "Enforcement node id"),
        ("Incident.FlowSourceAddress", "Flow source (optional, for correlation)"),
        ("Incident.FlowDestinationAddress", "Flow destination"),
        ("Incident.FlowProtocol", "Flow protocol"),
    ];

    [Fact]
    public void Ac1IncidentTextBoxesExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        foreach ((string binding, string name) in Fields)
        {
            string marker = $"Text=\"{{Binding {binding}}}\" Classes=\"mfc-field\" PlaceholderText=";
            int idx = main.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(idx >= 0, binding);
            string slice = main.Substring(idx, Math.Min(240, main.Length - idx));
            Assert.Contains($"AutomationProperties.Name=\"{name}\"", slice, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2Plan16AndTestingDocLockA11y01()
    {
        string root = FindRepoRoot();
        string plan16 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-16-desktop-incident-automation-properties.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-01", plan16, StringComparison.Ordinal);
        Assert.Contains("W7-160 DONE", plan16, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentAutomationNameLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-160 Living Spec lock)", limitations, StringComparison.Ordinal);
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
