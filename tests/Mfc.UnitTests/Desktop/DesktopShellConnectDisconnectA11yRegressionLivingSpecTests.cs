using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-CONN-02 / W7-177: Connect/Disconnect + Incident action Names; PLAN-19 COMPLETE.</summary>
public sealed class DesktopShellConnectDisconnectA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1ConnectDisconnectAndIncidentActionNamesMatrixLocked()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.DoesNotContain("Watermark=", main, StringComparison.Ordinal);

        int connect = main.IndexOf("Command=\"{Binding ConnectCommand}\"", StringComparison.Ordinal);
        Assert.True(connect > 0);
        string connectBefore = main.Substring(Math.Max(0, connect - 200), Math.Min(200, connect));
        Assert.Contains("AutomationProperties.Name=\"Connect\"", connectBefore, StringComparison.Ordinal);

        int disconnect = main.IndexOf("Command=\"{Binding DisconnectCommand}\"", StringComparison.Ordinal);
        Assert.True(disconnect > 0);
        string disconnectBefore = main.Substring(Math.Max(0, disconnect - 200), Math.Min(200, disconnect));
        Assert.Contains("AutomationProperties.Name=\"Disconnect\"", disconnectBefore, StringComparison.Ordinal);

        int ingest = main.IndexOf("Incident.IngestCommand", StringComparison.Ordinal);
        Assert.True(ingest > 0);
        string ingestBefore = main.Substring(Math.Max(0, ingest - 280), Math.Min(280, ingest));
        Assert.Contains("AutomationProperties.Name=\"Ingest signal\"", ingestBefore, StringComparison.Ordinal);

        int bind = main.IndexOf("Incident.BindAssessmentCommand", StringComparison.Ordinal);
        Assert.True(bind > 0);
        string bindBefore = main.Substring(Math.Max(0, bind - 280), Math.Min(280, bind));
        Assert.Contains("AutomationProperties.Name=\"Bind assessment\"", bindBefore, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2Plan19CompleteAndDocsLockA11yConn02()
    {
        string root = FindRepoRoot();
        string plan19 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-19-desktop-shell-connect-disconnect-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("PLAN-19 COMPLETE", plan19, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CONN-02", plan19, StringComparison.Ordinal);
        Assert.Contains("W7-177 DONE", plan19, StringComparison.Ordinal);
        Assert.Contains("DesktopShellConnectDisconnectA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-177 Living Spec lock)", limitations, StringComparison.Ordinal);
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
