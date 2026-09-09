using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-ACTION-02 / W7-167: Bind Name + Incident field Names matrix; PLAN-17 COMPLETE.</summary>
public sealed class DesktopIncidentBindActionA11yRegressionLivingSpecTests
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
    public void Ac1BindNameAndIncidentFieldNamesMatrixLocked()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.DoesNotContain("Watermark=", main, StringComparison.Ordinal);
        Assert.Contains("Content=\"Bind assessment\"", main, StringComparison.Ordinal);
        int cmd = main.IndexOf("Incident.BindAssessmentCommand", StringComparison.Ordinal);
        Assert.True(cmd > 0);
        string before = main.Substring(Math.Max(0, cmd - 280), Math.Min(280, cmd));
        Assert.Contains("AutomationProperties.Name=\"Bind assessment\"", before, StringComparison.Ordinal);

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
    public void Ac2Plan17CompleteAndDocsLockA11yAction02()
    {
        string root = FindRepoRoot();
        string plan17 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-17-desktop-incident-bind-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("PLAN-17 COMPLETE", plan17, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-ACTION-02", plan17, StringComparison.Ordinal);
        Assert.Contains("W7-167 DONE", plan17, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentBindActionA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-167 Living Spec lock)", limitations, StringComparison.Ordinal);
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
