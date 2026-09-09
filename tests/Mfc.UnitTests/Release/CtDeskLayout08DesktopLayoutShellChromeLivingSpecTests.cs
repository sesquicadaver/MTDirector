using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-LAYOUT-08 / W7-143: Shell chrome layout Living Spec is present and documented.</summary>
public sealed class CtDeskLayout08DesktopLayoutShellChromeLivingSpecTests
{
    [Fact]
    public void Ac1DesktopLayoutShellChromeLivingSpecAndPlan13MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopLayoutShellChromeLivingSpecTests.cs");
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopLayoutShellChromeLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac1ShellChromeUsesAutoSplitterColumnsBetweenPanes", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ShellChromePanesHaveMinWidthFloors", body, StringComparison.Ordinal);
        Assert.Contains("W7-143 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-08", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutShellChromeLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-143 Living Spec lock)", limitations, StringComparison.Ordinal);
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
