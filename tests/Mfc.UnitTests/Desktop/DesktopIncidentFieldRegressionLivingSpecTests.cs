using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-FIELD-02 / W7-157: Incident TextBoxes retain PlaceholderText + mfc-field; PLAN-15 COMPLETE.</summary>
public sealed class DesktopIncidentFieldRegressionLivingSpecTests
{
    private static readonly string[] IncidentBindings =
    [
        "Incident.SourceEventId",
        "Incident.Category",
        "Incident.DeduplicationKey",
        "Incident.Confidence",
        "Incident.EndpointIdText",
        "Incident.PresenceIdText",
        "Incident.EnforcementNodeIdText",
        "Incident.FlowSourceAddress",
        "Incident.FlowDestinationAddress",
        "Incident.FlowProtocol",
    ];

    [Fact]
    public void Ac1IncidentTextBoxesRetainPlaceholderTextAndMfcField()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.DoesNotContain("Watermark=", main, StringComparison.Ordinal);
        foreach (string binding in IncidentBindings)
        {
            Assert.Contains($"Text=\"{{Binding {binding}}}\" Classes=\"mfc-field\" PlaceholderText=", main, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2Plan15CompleteAndDocsLockField02()
    {
        string root = FindRepoRoot();
        string plan15 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-15-desktop-incident-mfc-field-style-hygiene.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("PLAN-15 COMPLETE", plan15, StringComparison.Ordinal);
        Assert.Contains("DESK-FIELD-02", plan15, StringComparison.Ordinal);
        Assert.Contains("W7-157 DONE", plan15, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentFieldRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-157 Living Spec lock)", limitations, StringComparison.Ordinal);
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
