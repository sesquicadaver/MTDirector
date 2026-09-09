using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-INGEST-02 / W7-172: Ingest Name + Bind Name + Incident field Names; PLAN-18 COMPLETE.</summary>
public sealed class DesktopIncidentIngestActionA11yRegressionLivingSpecTests
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
    public void Ac1IngestBindAndIncidentFieldNamesMatrixLocked()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.DoesNotContain("Watermark=", main, StringComparison.Ordinal);

        Assert.Contains("Content=\"Ingest signal\"", main, StringComparison.Ordinal);
        int ingest = main.IndexOf("Incident.IngestCommand", StringComparison.Ordinal);
        Assert.True(ingest > 0);
        string ingestBefore = main.Substring(Math.Max(0, ingest - 280), Math.Min(280, ingest));
        Assert.Contains("AutomationProperties.Name=\"Ingest signal\"", ingestBefore, StringComparison.Ordinal);

        Assert.Contains("Content=\"Bind assessment\"", main, StringComparison.Ordinal);
        int bind = main.IndexOf("Incident.BindAssessmentCommand", StringComparison.Ordinal);
        Assert.True(bind > 0);
        string bindBefore = main.Substring(Math.Max(0, bind - 280), Math.Min(280, bind));
        Assert.Contains("AutomationProperties.Name=\"Bind assessment\"", bindBefore, StringComparison.Ordinal);

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
    public void Ac2Plan18CompleteAndDocsLockA11yIngest02()
    {
        string root = FindRepoRoot();
        string plan18 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-18-desktop-incident-ingest-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("PLAN-18 COMPLETE", plan18, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INGEST-02", plan18, StringComparison.Ordinal);
        Assert.Contains("W7-172 DONE", plan18, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentIngestActionA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-172 Living Spec lock)", limitations, StringComparison.Ordinal);
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
