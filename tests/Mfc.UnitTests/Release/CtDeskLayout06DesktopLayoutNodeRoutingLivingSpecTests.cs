using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-LAYOUT-06 / W7-139: Node + RoutingAssurance layout Living Spec is present and documented.</summary>
public sealed class CtDeskLayout06DesktopLayoutNodeRoutingLivingSpecTests
{
    [Fact]
    public void Ac1DesktopLayoutNodeRoutingLivingSpecAndPlan13MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopLayoutNodeRoutingLivingSpecTests.cs");
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopLayoutNodeRoutingLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac1NodeAndRoutingListsUseListMinHeightAndNoMaxHeightCaps", body, StringComparison.Ordinal);
        Assert.Contains("Ac2NodeAndRoutingPrimaryListsAllBindListMinHeight", body, StringComparison.Ordinal);
        Assert.Contains("W7-139 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-06", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutNodeRoutingLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-139 Living Spec lock)", limitations, StringComparison.Ordinal);
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
