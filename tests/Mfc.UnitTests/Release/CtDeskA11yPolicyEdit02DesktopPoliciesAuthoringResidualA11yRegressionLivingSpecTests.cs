using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-POLICY-EDIT-02 / W7-187 authoring residual a11y regression; PLAN-21 COMPLETE.</summary>
public sealed class CtDeskA11yPolicyEdit02DesktopPoliciesAuthoringResidualA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesAuthoringResidualA11yRegressionLivingSpecAndPlan21CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesAuthoringResidualA11yRegressionLivingSpecTests.cs")));
        string plan21 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-21-desktop-policies-authoring-residual-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-21 COMPLETE", plan21, StringComparison.Ordinal);
        Assert.Contains("W7-187 DONE", plan21, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesAuthoringResidualA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yPolicyEdit02", testing, StringComparison.Ordinal);
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
