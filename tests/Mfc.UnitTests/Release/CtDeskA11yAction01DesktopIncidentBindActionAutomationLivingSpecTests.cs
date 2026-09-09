using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-ACTION-01 / W7-165 Bind assessment AutomationProperties.Name Living Spec.</summary>
public sealed class CtDeskA11yAction01DesktopIncidentBindActionAutomationLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentBindActionAutomationLivingSpecAndPlan17MatrixExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentBindActionAutomationLivingSpecTests.cs")));
        string plan17 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-17-desktop-incident-bind-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("DESK-A11Y-ACTION-01", plan17, StringComparison.Ordinal);
        Assert.Contains("W7-165 DONE", plan17, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentBindActionAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yAction01", testing, StringComparison.Ordinal);
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
