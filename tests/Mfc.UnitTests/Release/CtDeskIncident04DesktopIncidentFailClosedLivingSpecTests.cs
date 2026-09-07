using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-INCIDENT-04 / W7-76: Desktop Incident fail-closed Living Spec; PLAN-06 COMPLETE.</summary>
public sealed class CtDeskIncident04DesktopIncidentFailClosedLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentFailClosedLivingSpecAndPlan06CompleteMatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentFailClosedLivingSpecTests.cs");
        string plan06 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-06-incident-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopIncidentFailClosedLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac1WireAndProtoStayIngestAndBindOnly", body, StringComparison.Ordinal);
        Assert.Contains("Ac2DesktopClientAndGrpcSourceExposeNoDeployOverlayFeedback", body, StringComparison.Ordinal);
        Assert.Contains("Ac3ViewModelFailClosedFlagsAndCommands", body, StringComparison.Ordinal);
        Assert.Contains("Ac4MainWindowIncidentSurfaceHasNoDeployOverlayActions", body, StringComparison.Ordinal);
        Assert.Contains("Ac5HostWireLockAndApplicationOnlySurfacesRemainDocumented", body, StringComparison.Ordinal);
        Assert.Contains("W7-76 DONE", plan06, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan06, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-04", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentFailClosedLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-76 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-06", limitations, StringComparison.Ordinal);
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
