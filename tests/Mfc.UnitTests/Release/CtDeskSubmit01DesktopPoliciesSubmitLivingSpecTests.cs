using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-SUBMIT-01 / W7-114: Desktop Policies Submit Living Spec is present and documented.</summary>
public sealed class CtDeskSubmit01DesktopPoliciesSubmitLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesSubmitLivingSpecAndPlan11MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesSubmitLivingSpecTests.cs");
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopPoliciesSubmitLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac2ViewModelExposesSubmitCommandAndLoadedRevisionSurface", body, StringComparison.Ordinal);
        Assert.Contains("Ac3SubmitCommandGuardsOnCanOperateAndRequiresLoadedRevisionInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac4PanelSubmitForReviewDelegatesToClientAndReturnsPanelState", body, StringComparison.Ordinal);
        Assert.Contains("Ac5MainWindowBindsPoliciesSubmitForReviewCommand", body, StringComparison.Ordinal);
        Assert.Contains("W7-114 DONE", plan11, StringComparison.Ordinal);
        Assert.Contains("DESK-SUBMIT-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesSubmitLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-114 Living Spec lock)", limitations, StringComparison.Ordinal);
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
