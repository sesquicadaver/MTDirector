using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-CONN-01 / W7-99: Desktop Connection Living Spec is present and documented.</summary>
public sealed class CtDeskConn01DesktopConnectionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopConnectionLivingSpecAndPlan09MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopConnectionLivingSpecTests.cs");
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopConnectionLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3StatusTextFormatsAllConnectionStatesAndShellSyncsFromService", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ShellExposesConnectDisconnectCommandsAndStatusSurface", body, StringComparison.Ordinal);
        Assert.Contains("Ac4ConnectDisconnectCanExecuteRulesAreFailClosed", body, StringComparison.Ordinal);
        Assert.Contains("Ac5MainWindowBindsConnectDisconnectAndStatus", body, StringComparison.Ordinal);
        Assert.Contains("W7-99 DONE", plan09, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopConnectionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-99 Living Spec lock)", limitations, StringComparison.Ordinal);
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
