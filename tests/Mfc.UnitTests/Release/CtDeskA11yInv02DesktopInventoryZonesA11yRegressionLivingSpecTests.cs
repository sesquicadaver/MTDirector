using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-INV-02 / W7-209 inv/zones a11y regression; PLAN-25 COMPLETE.</summary>
public sealed class CtDeskA11yInv02DesktopInventoryZonesA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopInventoryZonesA11yRegressionLivingSpecAndPlan25CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopInventoryZonesA11yRegressionLivingSpecTests.cs")));
        string plan25 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-25-desktop-inventory-zones-add-router-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-25 COMPLETE", plan25, StringComparison.Ordinal);
        Assert.Contains("W7-209 DONE", plan25, StringComparison.Ordinal);
        Assert.Contains("DesktopInventoryZonesA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yInv02", testing, StringComparison.Ordinal);
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
