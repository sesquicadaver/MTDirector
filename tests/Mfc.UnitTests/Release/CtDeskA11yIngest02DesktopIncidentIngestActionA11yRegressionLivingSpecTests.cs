using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-INGEST-02 / W7-172 ingest-action a11y regression; PLAN-18 COMPLETE.</summary>
public sealed class CtDeskA11yIngest02DesktopIncidentIngestActionA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentIngestActionA11yRegressionLivingSpecAndPlan18CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentIngestActionA11yRegressionLivingSpecTests.cs")));
        string plan18 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-18-desktop-incident-ingest-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-18 COMPLETE", plan18, StringComparison.Ordinal);
        Assert.Contains("W7-172 DONE", plan18, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentIngestActionA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yIngest02", testing, StringComparison.Ordinal);
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
