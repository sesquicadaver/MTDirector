using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-PLACEHOLDER-02 / W7-152 Watermark residue Living Spec; PLAN-14 COMPLETE.</summary>
public sealed class CtDeskPlaceholder02DesktopWatermarkResidueLivingSpecTests
{
    [Fact]
    public void Ac1DesktopWatermarkResidueLivingSpecAndPlan14CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopWatermarkResidueLivingSpecTests.cs")));
        string plan14 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-14-desktop-avalonia-placeholder-incident-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-14 COMPLETE", plan14, StringComparison.Ordinal);
        Assert.Contains("W7-152 DONE", plan14, StringComparison.Ordinal);
        Assert.Contains("DesktopWatermarkResidueLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskPlaceholder02", testing, StringComparison.Ordinal);
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
