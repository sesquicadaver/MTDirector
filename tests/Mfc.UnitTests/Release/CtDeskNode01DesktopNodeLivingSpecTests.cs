using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-NODE-01 / W7-90: Desktop Node VRRP Living Spec is present and documented.</summary>
public sealed class CtDeskNode01DesktopNodeLivingSpecTests
{
    [Fact]
    public void Ac1DesktopNodeLivingSpecAndPlan08MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopNodeLivingSpecTests.cs");
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopNodeLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3ValidateVrrpPairLoadsFindingsWhenVrrpNodeConnected", body, StringComparison.Ordinal);
        Assert.Contains("Ac2NodeViewModelExposesVrrpValidateCaptureAndFindings", body, StringComparison.Ordinal);
        Assert.Contains("Ac4ValidateVrrpRequiresVrrpNodeAndConnectedController", body, StringComparison.Ordinal);
        Assert.Contains("Ac6HostAndProtoContractRemainPresentForInventoryVrrp", body, StringComparison.Ordinal);
        Assert.Contains("W7-90 DONE", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-NODE-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopNodeLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-90 Living Spec lock)", limitations, StringComparison.Ordinal);
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
