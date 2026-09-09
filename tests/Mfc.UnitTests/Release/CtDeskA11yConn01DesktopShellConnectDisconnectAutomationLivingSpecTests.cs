using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-CONN-01 / W7-175 Connect/Disconnect AutomationProperties.Name Living Spec.</summary>
public sealed class CtDeskA11yConn01DesktopShellConnectDisconnectAutomationLivingSpecTests
{
    [Fact]
    public void Ac1DesktopShellConnectDisconnectAutomationLivingSpecAndPlan19MatrixExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopShellConnectDisconnectAutomationLivingSpecTests.cs")));
        string plan19 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-19-desktop-shell-connect-disconnect-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("DESK-A11Y-CONN-01", plan19, StringComparison.Ordinal);
        Assert.Contains("W7-175 DONE", plan19, StringComparison.Ordinal);
        Assert.Contains("DesktopShellConnectDisconnectAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yConn01", testing, StringComparison.Ordinal);
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
