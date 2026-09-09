using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-ACTION-01 / W7-165: Bind assessment exposes AutomationProperties.Name.</summary>
public sealed class DesktopIncidentBindActionAutomationLivingSpecTests
{
    [Fact]
    public void Ac1BindAssessmentButtonExposesAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Content=\"Bind assessment\"", main, StringComparison.Ordinal);
        Assert.Contains("Incident.BindAssessmentCommand", main, StringComparison.Ordinal);
        int cmd = main.IndexOf("Incident.BindAssessmentCommand", StringComparison.Ordinal);
        Assert.True(cmd > 0);
        string before = main.Substring(Math.Max(0, cmd - 280), Math.Min(280, cmd));
        Assert.Contains("AutomationProperties.Name=\"Bind assessment\"", before, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2Plan17AndTestingDocLockA11yAction01()
    {
        string root = FindRepoRoot();
        string plan17 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-17-desktop-incident-bind-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-ACTION-01", plan17, StringComparison.Ordinal);
        Assert.Contains("W7-165 DONE", plan17, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentBindActionAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-165 Living Spec lock)", limitations, StringComparison.Ordinal);
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
