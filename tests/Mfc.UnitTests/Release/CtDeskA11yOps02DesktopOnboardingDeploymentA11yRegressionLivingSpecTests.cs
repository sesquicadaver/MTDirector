using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-OPS-02 / W7-202 ops a11y regression; PLAN-24 COMPLETE.</summary>
public sealed class CtDeskA11yOps02DesktopOnboardingDeploymentA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopOnboardingDeploymentA11yRegressionLivingSpecAndPlan24CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopOnboardingDeploymentA11yRegressionLivingSpecTests.cs")));
        string plan24 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-24-desktop-onboarding-deployment-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-24 COMPLETE", plan24, StringComparison.Ordinal);
        Assert.Contains("W7-202 DONE", plan24, StringComparison.Ordinal);
        Assert.Contains("DesktopOnboardingDeploymentA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yOps02", testing, StringComparison.Ordinal);
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
