using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-CONN-02 / W7-177 shell connection a11y regression; PLAN-19 COMPLETE.</summary>
public sealed class CtDeskA11yConn02DesktopShellConnectDisconnectA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopShellConnectDisconnectA11yRegressionLivingSpecAndPlan19CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopShellConnectDisconnectA11yRegressionLivingSpecTests.cs")));
        string plan19 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-19-desktop-shell-connect-disconnect-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-19 COMPLETE", plan19, StringComparison.Ordinal);
        Assert.Contains("W7-177 DONE", plan19, StringComparison.Ordinal);
        Assert.Contains("DesktopShellConnectDisconnectA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yConn02", testing, StringComparison.Ordinal);
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
