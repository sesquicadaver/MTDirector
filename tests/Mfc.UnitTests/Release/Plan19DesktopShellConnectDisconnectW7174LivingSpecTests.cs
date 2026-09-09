using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-174: PLAN-19 inventory documents ranked DESK-A11Y-CONN-* rows and seeds DESK-A11Y-CONN-01.</summary>
public sealed class Plan19DesktopShellConnectDisconnectW7174LivingSpecTests
{
    [Fact]
    public void Ac1Plan19InventoryDocumentsRankedRowsAndSeedsDeskA11yConn01()
    {
        string root = RepoRoot();
        string plan19 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-19-desktop-shell-connect-disconnect-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan18 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-18-desktop-incident-ingest-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-19 — Desktop shell Connect/Disconnect AutomationProperties Living Spec product tranche", plan19, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CONN-01", plan19, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CONN-02", plan19, StringComparison.Ordinal);
        Assert.Contains("W7-175", plan19, StringComparison.Ordinal);
        Assert.Contains("Connect", plan19, StringComparison.Ordinal);
        Assert.Contains("Disconnect", plan19, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan19, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-174 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-19", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CONN-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-175", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-19", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-19-desktop-shell-connect-disconnect-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-19-desktop-shell-connect-disconnect-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan18, StringComparison.Ordinal);
        Assert.Contains("PLAN-19", plan18, StringComparison.Ordinal);
        Assert.Contains("Plan19DesktopShellConnectDisconnectW7174", testing, StringComparison.Ordinal);
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
