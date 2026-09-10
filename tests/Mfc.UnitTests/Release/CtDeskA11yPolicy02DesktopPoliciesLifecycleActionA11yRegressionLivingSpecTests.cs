using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-POLICY-02 / W7-182 Policies lifecycle a11y regression; PLAN-20 COMPLETE.</summary>
public sealed class CtDeskA11yPolicy02DesktopPoliciesLifecycleActionA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesLifecycleActionA11yRegressionLivingSpecAndPlan20CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLifecycleActionA11yRegressionLivingSpecTests.cs")));
        string plan20 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-20-desktop-policies-lifecycle-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-20 COMPLETE", plan20, StringComparison.Ordinal);
        Assert.Contains("W7-182 DONE", plan20, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesLifecycleActionA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yPolicy02", testing, StringComparison.Ordinal);
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
