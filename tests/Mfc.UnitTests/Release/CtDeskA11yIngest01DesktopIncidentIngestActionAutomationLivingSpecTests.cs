using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-INGEST-01 / W7-170 Ingest signal AutomationProperties.Name Living Spec.</summary>
public sealed class CtDeskA11yIngest01DesktopIncidentIngestActionAutomationLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentIngestActionAutomationLivingSpecAndPlan18MatrixExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentIngestActionAutomationLivingSpecTests.cs")));
        string plan18 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-18-desktop-incident-ingest-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("DESK-A11Y-INGEST-01", plan18, StringComparison.Ordinal);
        Assert.Contains("W7-170 DONE", plan18, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentIngestActionAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yIngest01", testing, StringComparison.Ordinal);
    }

    private static string RepoRoot()
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
