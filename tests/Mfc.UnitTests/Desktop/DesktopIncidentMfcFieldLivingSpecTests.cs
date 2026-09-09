using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-FIELD-01 / W7-155: Incident TextBoxes use Classes=mfc-field.</summary>
public sealed class DesktopIncidentMfcFieldLivingSpecTests
{
    [Fact]
    public void Ac1IncidentTextBoxesUseMfcFieldClass()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        string[] bindings =
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
        foreach (string binding in bindings)
        {
            Assert.Contains($"Text=\"{{Binding {binding}}}\" Classes=\"mfc-field\"", main, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2Plan15AndTestingDocLockField01()
    {
        string root = FindRepoRoot();
        string plan15 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-15-desktop-incident-mfc-field-style-hygiene.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-FIELD-01", plan15, StringComparison.Ordinal);
        Assert.Contains("W7-155 DONE", plan15, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentMfcFieldLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-155 Living Spec lock)", limitations, StringComparison.Ordinal);
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
