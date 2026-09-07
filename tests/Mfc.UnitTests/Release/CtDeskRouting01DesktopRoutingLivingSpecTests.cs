using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-ROUTING-01 / W7-70: Desktop Routing assurance Living Spec deepened vs RoutingAssuranceGrpcHost.</summary>
public sealed class CtDeskRouting01DesktopRoutingLivingSpecTests
{
    [Fact]
    public void Ac1DesktopRoutingHostAlignmentAndPlan05MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopRoutingAssuranceLivingSpecTests.cs");
        string plan05 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-05-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopRoutingAssuranceLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac11RefreshLoadsGetPayloadForSelectedDevice", body, StringComparison.Ordinal);
        Assert.Contains("Ac10DesktopClientIsGetOnlyAlignedWithWire", body, StringComparison.Ordinal);
        Assert.Contains("Ac13HostContractLivingSpecRemainsPresent", body, StringComparison.Ordinal);
        Assert.Contains("W7-70 DONE", plan05, StringComparison.Ordinal);
        Assert.Contains("DESK-ROUTING-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopRoutingAssuranceLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-70 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-05", plan05, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan05, StringComparison.Ordinal);
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
