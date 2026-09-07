using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-POLICY-01 / W7-79: Desktop Policies Living Spec is present and documented.</summary>
public sealed class CtDeskPolicy01DesktopPoliciesLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesLivingSpecAndPlan07MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs");
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopPoliciesLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3RefreshCatalogLoadsPoliciesWhenConnected", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ViewModelExposesCatalogAuthoringReviewBindAndFailClosedDeploy", body, StringComparison.Ordinal);
        Assert.Contains("Ac4CatalogRequiresConnectedControllerAndDeployStaysFailClosed", body, StringComparison.Ordinal);
        Assert.Contains("Ac6HostContractLivingSpecRemainsPresent", body, StringComparison.Ordinal);
        Assert.Contains("W7-79 DONE", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-POLICY-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-79 Living Spec lock)", limitations, StringComparison.Ordinal);
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
