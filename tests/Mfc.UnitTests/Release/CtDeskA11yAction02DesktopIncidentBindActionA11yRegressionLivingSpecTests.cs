using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-ACTION-02 / W7-167 bind-action a11y regression; PLAN-17 COMPLETE.</summary>
public sealed class CtDeskA11yAction02DesktopIncidentBindActionA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentBindActionA11yRegressionLivingSpecAndPlan17CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentBindActionA11yRegressionLivingSpecTests.cs")));
        string plan17 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-17-desktop-incident-bind-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-17 COMPLETE", plan17, StringComparison.Ordinal);
        Assert.Contains("W7-167 DONE", plan17, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentBindActionA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yAction02", testing, StringComparison.Ordinal);
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
