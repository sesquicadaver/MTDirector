using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-FIELD-01 / W7-155 Incident mfc-field Living Spec.</summary>
public sealed class CtDeskField01DesktopIncidentMfcFieldLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentMfcFieldLivingSpecAndPlan15MatrixExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentMfcFieldLivingSpecTests.cs")));
        string plan15 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-15-desktop-incident-mfc-field-style-hygiene.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("DESK-FIELD-01", plan15, StringComparison.Ordinal);
        Assert.Contains("W7-155 DONE", plan15, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentMfcFieldLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskField01", testing, StringComparison.Ordinal);
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
