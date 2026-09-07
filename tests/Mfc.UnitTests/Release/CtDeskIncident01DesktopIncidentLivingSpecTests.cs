using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-INCIDENT-01 / W7-73: Desktop Incident ViewModel + client Living Spec vs IncidentGrpcHost.</summary>
public sealed class CtDeskIncident01DesktopIncidentLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentLivingSpecAndPlan06MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentLivingSpecTests.cs");
        string plan06 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-06-incident-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopIncidentLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac1WireAndDesktopClientExposeIngestAndBindOnly", body, StringComparison.Ordinal);
        Assert.Contains("Ac4IngestMapsLastSignalFromClientPayload", body, StringComparison.Ordinal);
        Assert.Contains("Ac5ShellWiresIncidentWithoutEighthNavigationModule", body, StringComparison.Ordinal);
        Assert.Contains("Ac6HostContractLivingSpecRemainsPresent", body, StringComparison.Ordinal);
        Assert.Contains("W7-73 DONE", plan06, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-73 Living Spec lock)", limitations, StringComparison.Ordinal);
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
