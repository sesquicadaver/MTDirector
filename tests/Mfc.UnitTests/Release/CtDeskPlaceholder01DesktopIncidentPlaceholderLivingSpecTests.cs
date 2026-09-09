using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-PLACEHOLDER-01 / W7-150 Incident PlaceholderText Living Spec.</summary>
public sealed class CtDeskPlaceholder01DesktopIncidentPlaceholderLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentPlaceholderLivingSpecAndPlan14MatrixExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentPlaceholderLivingSpecTests.cs")));
        string plan14 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-14-desktop-avalonia-placeholder-incident-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("DESK-PLACEHOLDER-01", plan14, StringComparison.Ordinal);
        Assert.Contains("W7-150 DONE", plan14, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentPlaceholderLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskPlaceholder01", testing, StringComparison.Ordinal);
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
