using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-126: PLAN-13 Desktop layout density inventory documents ranked DESK-LAYOUT-* rows and seeds DESK-LAYOUT-00.</summary>
public sealed class Plan13DesktopLayoutDensityW7126LivingSpecTests
{
    [Fact]
    public void Ac1Plan13InventoryDocumentsRankedRowsAndSeedsDeskLayout00()
    {
        string root = RepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan12 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-12-desktop-policies-residual-lifecycle.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-13 — Desktop layout density Living Spec product tranche", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-00", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-01", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-10", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-127", plan13, StringComparison.Ordinal);
        Assert.Contains("Mfc.ListMinHeight", plan13, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan13, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-126 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-13", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-00", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-127", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-13", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-13-desktop-layout-density.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-13-desktop-layout-density.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan12, StringComparison.Ordinal);
        Assert.Contains("PLAN-13", plan12, StringComparison.Ordinal);
        Assert.Contains("Plan13DesktopLayoutDensityW7126", testing, StringComparison.Ordinal);
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
