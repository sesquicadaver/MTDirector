using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-01 / W7-160 Incident AutomationProperties.Name Living Spec.</summary>
public sealed class CtDeskA11y01DesktopIncidentAutomationNameLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentAutomationNameLivingSpecAndPlan16MatrixExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentAutomationNameLivingSpecTests.cs")));
        string plan16 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-16-desktop-incident-automation-properties.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("DESK-A11Y-01", plan16, StringComparison.Ordinal);
        Assert.Contains("W7-160 DONE", plan16, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentAutomationNameLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11y01", testing, StringComparison.Ordinal);
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
