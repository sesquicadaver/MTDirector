using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-PROBE-01 / W7-94: Desktop Probe Living Spec is present and documented.</summary>
public sealed class CtDeskProbe01DesktopProbeLivingSpecTests
{
    [Fact]
    public void Ac1DesktopProbeLivingSpecAndPlan08MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopProbeLivingSpecTests.cs");
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopProbeLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3ProbeShowsIdentitySupportAndMutatedWithoutRegister", body, StringComparison.Ordinal);
        Assert.Contains("Ac2WizardExposesProbeCommandAndResultSurface", body, StringComparison.Ordinal);
        Assert.Contains("Ac4ProbeRequiresDeviceAndConnectedController", body, StringComparison.Ordinal);
        Assert.Contains("Ac5MainWindowBindsProbeCommandAndResult", body, StringComparison.Ordinal);
        Assert.Contains("W7-94 DONE", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-PROBE-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopProbeLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-94 Living Spec lock)", limitations, StringComparison.Ordinal);
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
