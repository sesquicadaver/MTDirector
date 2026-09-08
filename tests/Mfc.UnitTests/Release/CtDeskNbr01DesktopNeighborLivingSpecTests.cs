using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-NBR-01 / W7-92: Desktop Neighbor Living Spec is present and documented.</summary>
public sealed class CtDeskNbr01DesktopNeighborLivingSpecTests
{
    [Fact]
    public void Ac1DesktopNeighborLivingSpecAndPlan08MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopNeighborLivingSpecTests.cs");
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopNeighborLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3LoadNeighborsFillsCandidatesFromSeedDeviceWithoutRegister", body, StringComparison.Ordinal);
        Assert.Contains("Ac2WizardExposesLoadApplyNeighborCommandsAndCandidates", body, StringComparison.Ordinal);
        Assert.Contains("Ac4LoadNeighborsRequiresSeedDeviceAndConnectedController", body, StringComparison.Ordinal);
        Assert.Contains("Ac5ApplyNeighborPrefillsHostWithoutRegisterAndMainWindowBinds", body, StringComparison.Ordinal);
        Assert.Contains("W7-92 DONE", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-NBR-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopNeighborLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-92 Living Spec lock)", limitations, StringComparison.Ordinal);
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
