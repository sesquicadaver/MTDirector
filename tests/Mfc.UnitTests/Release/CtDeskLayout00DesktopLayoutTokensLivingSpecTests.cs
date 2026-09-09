using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-LAYOUT-00 / W7-127: layout tokens Living Spec is present and documented.</summary>
public sealed class CtDeskLayout00DesktopLayoutTokensLivingSpecTests
{
    [Fact]
    public void Ac1DesktopLayoutTokensLivingSpecAndPlan13MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopLayoutTokensLivingSpecTests.cs");
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string layoutDoc = Path.Combine(root, "docs/development/desktop-layout.md");

        Assert.True(File.Exists(deskTests), "DesktopLayoutTokensLivingSpecTests.cs missing.");
        Assert.True(File.Exists(layoutDoc), "docs/development/desktop-layout.md missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac1AppAxamlDefinesSharedLayoutHeightTokens", body, StringComparison.Ordinal);
        Assert.Contains("Ac2MainWindowBindsTokensOnPrimarySnapshotListAndDetail", body, StringComparison.Ordinal);
        Assert.Contains("W7-127 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-00", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutTokensLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-127 Living Spec lock)", limitations, StringComparison.Ordinal);
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
