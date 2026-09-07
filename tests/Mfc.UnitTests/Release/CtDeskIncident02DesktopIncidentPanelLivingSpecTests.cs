using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-INCIDENT-02 / W7-74: Desktop Incident MainWindow panel Living Spec.</summary>
public sealed class CtDeskIncident02DesktopIncidentPanelLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentPanelLivingSpecAndPlan06MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentPanelLivingSpecTests.cs");
        string plan06 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-06-incident-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopIncidentPanelLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac1MainWindowBindsIncidentIngestFormStatusAndEmptyState", body, StringComparison.Ordinal);
        Assert.Contains("Ac2PanelStaysUnderOperationsWithoutEighthNavigationModule", body, StringComparison.Ordinal);
        Assert.Contains("Ac3EmptyStateAndLastSignalSurfaceExistOnViewModel", body, StringComparison.Ordinal);
        Assert.Contains("Ac4HostContractLivingSpecRemainsPresent", body, StringComparison.Ordinal);
        Assert.Contains("W7-74 DONE", plan06, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-02", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentPanelLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-74 Living Spec lock)", limitations, StringComparison.Ordinal);
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
