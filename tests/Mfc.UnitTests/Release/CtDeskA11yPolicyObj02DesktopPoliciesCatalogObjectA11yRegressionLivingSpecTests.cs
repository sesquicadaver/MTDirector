using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-POLICY-OBJ-02 / W7-197 catalog/object a11y regression; PLAN-23 COMPLETE.</summary>
public sealed class CtDeskA11yPolicyObj02DesktopPoliciesCatalogObjectA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesCatalogObjectA11yRegressionLivingSpecAndPlan23CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesCatalogObjectA11yRegressionLivingSpecTests.cs")));
        string plan23 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-23-desktop-policies-catalog-object-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-23 COMPLETE", plan23, StringComparison.Ordinal);
        Assert.Contains("W7-197 DONE", plan23, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesCatalogObjectA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yPolicyObj02", testing, StringComparison.Ordinal);
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
