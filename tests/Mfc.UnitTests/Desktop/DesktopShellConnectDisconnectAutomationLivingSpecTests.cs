using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-CONN-01 / W7-175: Connect/Disconnect expose AutomationProperties.Name.</summary>
public sealed class DesktopShellConnectDisconnectAutomationLivingSpecTests
{
    [Fact]
    public void Ac1ConnectAndDisconnectButtonsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Content=\"Connect\"", main, StringComparison.Ordinal);
        Assert.Contains("ConnectCommand", main, StringComparison.Ordinal);
        int connect = main.IndexOf("Command=\"{Binding ConnectCommand}\"", StringComparison.Ordinal);
        Assert.True(connect > 0);
        string connectBefore = main.Substring(Math.Max(0, connect - 200), Math.Min(200, connect));
        Assert.Contains("AutomationProperties.Name=\"Connect\"", connectBefore, StringComparison.Ordinal);

        Assert.Contains("Content=\"Disconnect\"", main, StringComparison.Ordinal);
        Assert.Contains("DisconnectCommand", main, StringComparison.Ordinal);
        int disconnect = main.IndexOf("Command=\"{Binding DisconnectCommand}\"", StringComparison.Ordinal);
        Assert.True(disconnect > 0);
        string disconnectBefore = main.Substring(Math.Max(0, disconnect - 200), Math.Min(200, disconnect));
        Assert.Contains("AutomationProperties.Name=\"Disconnect\"", disconnectBefore, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2Plan19AndTestingDocLockA11yConn01()
    {
        string root = FindRepoRoot();
        string plan19 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-19-desktop-shell-connect-disconnect-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-CONN-01", plan19, StringComparison.Ordinal);
        Assert.Contains("W7-175 DONE", plan19, StringComparison.Ordinal);
        Assert.Contains("DesktopShellConnectDisconnectAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-175 Living Spec lock)", limitations, StringComparison.Ordinal);
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
