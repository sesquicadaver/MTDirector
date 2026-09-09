using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-02 / W7-162 a11y regression Living Spec; PLAN-16 COMPLETE.</summary>
public sealed class CtDeskA11y02DesktopIncidentA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentA11yRegressionLivingSpecAndPlan16CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentA11yRegressionLivingSpecTests.cs")));
        string plan16 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-16-desktop-incident-automation-properties.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-16 COMPLETE", plan16, StringComparison.Ordinal);
        Assert.Contains("W7-162 DONE", plan16, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11y02", testing, StringComparison.Ordinal);
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
