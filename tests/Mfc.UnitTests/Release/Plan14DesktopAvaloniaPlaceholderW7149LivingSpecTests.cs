using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-149: PLAN-14 inventory documents ranked DESK-PLACEHOLDER-* rows and seeds DESK-PLACEHOLDER-01.</summary>
public sealed class Plan14DesktopAvaloniaPlaceholderW7149LivingSpecTests
{
    [Fact]
    public void Ac1Plan14InventoryDocumentsRankedRowsAndSeedsDeskPlaceholder01()
    {
        string root = RepoRoot();
        string plan14 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-14-desktop-avalonia-placeholder-incident-surface.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-14 — Desktop Avalonia PlaceholderText / Incident surface Living Spec product tranche", plan14, StringComparison.Ordinal);
        Assert.Contains("DESK-PLACEHOLDER-01", plan14, StringComparison.Ordinal);
        Assert.Contains("DESK-PLACEHOLDER-02", plan14, StringComparison.Ordinal);
        Assert.Contains("W7-150", plan14, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText", plan14, StringComparison.Ordinal);
        Assert.Contains("Watermark", plan14, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan14, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-149 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-14", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-PLACEHOLDER-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-150", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-14", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-14-desktop-avalonia-placeholder-incident-surface.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-14-desktop-avalonia-placeholder-incident-surface.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan13, StringComparison.Ordinal);
        Assert.Contains("PLAN-14", plan13, StringComparison.Ordinal);
        Assert.Contains("Plan14DesktopAvaloniaPlaceholderW7149", testing, StringComparison.Ordinal);
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
