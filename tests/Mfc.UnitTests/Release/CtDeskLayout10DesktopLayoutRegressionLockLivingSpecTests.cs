using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-LAYOUT-10 / W7-147: PLAN-13 regression lock Living Spec is present and PLAN-13 COMPLETE.</summary>
public sealed class CtDeskLayout10DesktopLayoutRegressionLockLivingSpecTests
{
    [Fact]
    public void Ac1DesktopLayoutRegressionLockLivingSpecAndPlan13CompleteExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopLayoutRegressionLockLivingSpecTests.cs");
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopLayoutRegressionLockLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac1AllDeskLayout00Through09LivingSpecsExistWithPrimaryAc", body, StringComparison.Ordinal);
        Assert.Contains("Ac2MainWindowHasNoPrimaryListMaxHeightCascadeResidue", body, StringComparison.Ordinal);
        Assert.Contains("W7-147 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("PLAN-13 COMPLETE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-10", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutRegressionLockLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-147 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-13 COMPLETE", limitations, StringComparison.Ordinal);
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
