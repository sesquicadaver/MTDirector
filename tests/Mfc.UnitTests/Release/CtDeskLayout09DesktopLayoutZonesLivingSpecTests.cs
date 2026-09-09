using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-LAYOUT-09 / W7-145: Zones layout Living Spec is present and documented.</summary>
public sealed class CtDeskLayout09DesktopLayoutZonesLivingSpecTests
{
    [Fact]
    public void Ac1DesktopLayoutZonesLivingSpecAndPlan13MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopLayoutZonesLivingSpecTests.cs");
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopLayoutZonesLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac1ZonesListsUseListMinHeightAndNoMaxHeightCaps", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ZonesPrimaryListsAndResolveFrameBindListMinHeight", body, StringComparison.Ordinal);
        Assert.Contains("W7-145 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-09", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutZonesLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-145 Living Spec lock)", limitations, StringComparison.Ordinal);
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
