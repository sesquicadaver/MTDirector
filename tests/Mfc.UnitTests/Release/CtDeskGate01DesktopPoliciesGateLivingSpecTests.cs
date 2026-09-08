using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-GATE-01 / W7-116: Desktop Policies Approve/Bind/Compile Living Spec is present and documented.</summary>
public sealed class CtDeskGate01DesktopPoliciesGateLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesGateLivingSpecAndPlan11MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesGateLivingSpecTests.cs");
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopPoliciesGateLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac2ViewModelExposesApproveBindCompileCommandsAndSurface", body, StringComparison.Ordinal);
        Assert.Contains("Ac3ApproveCommandRequiresAnalysisRunAndCallsPanelInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac4BindAndCompileCommandsGuardAndCallPanelInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac5PanelApproveBindCompileDelegateToClientWithoutLocalSemanticEngine", body, StringComparison.Ordinal);
        Assert.Contains("W7-116 DONE", plan11, StringComparison.Ordinal);
        Assert.Contains("PLAN-11 COMPLETE", plan11, StringComparison.Ordinal);
        Assert.Contains("DESK-GATE-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesGateLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-116 Living Spec lock)", limitations, StringComparison.Ordinal);
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
