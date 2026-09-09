using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-INGEST-01 / W7-170: Ingest signal exposes AutomationProperties.Name.</summary>
public sealed class DesktopIncidentIngestActionAutomationLivingSpecTests
{
    [Fact]
    public void Ac1IngestSignalButtonExposesAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Content=\"Ingest signal\"", main, StringComparison.Ordinal);
        Assert.Contains("Incident.IngestCommand", main, StringComparison.Ordinal);
        int cmd = main.IndexOf("Incident.IngestCommand", StringComparison.Ordinal);
        Assert.True(cmd > 0);
        string before = main.Substring(Math.Max(0, cmd - 280), Math.Min(280, cmd));
        Assert.Contains("AutomationProperties.Name=\"Ingest signal\"", before, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2Plan18AndTestingDocLockA11yIngest01()
    {
        string root = FindRepoRoot();
        string plan18 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-18-desktop-incident-ingest-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-INGEST-01", plan18, StringComparison.Ordinal);
        Assert.Contains("W7-170 DONE", plan18, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentIngestActionAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-170 Living Spec lock)", limitations, StringComparison.Ordinal);
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
