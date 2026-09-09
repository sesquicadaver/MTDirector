using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-FIELD-02 / W7-157 field regression Living Spec; PLAN-15 COMPLETE.</summary>
public sealed class CtDeskField02DesktopIncidentFieldRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentFieldRegressionLivingSpecAndPlan15CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentFieldRegressionLivingSpecTests.cs")));
        string plan15 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-15-desktop-incident-mfc-field-style-hygiene.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-15 COMPLETE", plan15, StringComparison.Ordinal);
        Assert.Contains("W7-157 DONE", plan15, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentFieldRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskField02", testing, StringComparison.Ordinal);
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
