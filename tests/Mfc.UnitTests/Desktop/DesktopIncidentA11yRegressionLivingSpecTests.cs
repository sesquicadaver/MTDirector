using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-02 / W7-162: Incident TextBoxes retain Name + PlaceholderText + mfc-field; PLAN-16 COMPLETE.</summary>
public sealed class DesktopIncidentA11yRegressionLivingSpecTests
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
    public void Ac1IncidentTextBoxesRetainNamePlaceholderAndMfcField()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.DoesNotContain("Watermark=", main, StringComparison.Ordinal);
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
    public void Ac2Plan16CompleteAndDocsLockA11y02()
    {
        string root = FindRepoRoot();
        string plan16 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-16-desktop-incident-automation-properties.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("PLAN-16 COMPLETE", plan16, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-02", plan16, StringComparison.Ordinal);
        Assert.Contains("W7-162 DONE", plan16, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-162 Living Spec lock)", limitations, StringComparison.Ordinal);
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
