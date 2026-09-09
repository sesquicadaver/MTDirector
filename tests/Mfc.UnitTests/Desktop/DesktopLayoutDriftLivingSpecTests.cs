using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-03 / W7-133: Drift events/findings/detail splitter Living Spec.</summary>
public sealed class DesktopLayoutDriftLivingSpecTests
{
    [Fact]
    public void Ac1DriftUsesThreeStarPanesWithTwoRowSplitters()
    {
        string slice = DriftSlice();
        Assert.Contains("RowDefinitions=\"Auto,*,Auto,*,Auto,*\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("IsVisible=\"{Binding IsDriftSelected}\" RowDefinitions=\"Auto,*,Auto,*\"", slice, StringComparison.Ordinal);
        Assert.Contains("Drift.Events", slice, StringComparison.Ordinal);
        Assert.Contains("Drift.SelectedEventFindings", slice, StringComparison.Ordinal);
        Assert.Contains("Drift.SemanticDiffText", slice, StringComparison.Ordinal);

        int splitterCount = 0;
        int idx = 0;
        while ((idx = slice.IndexOf("<GridSplitter", idx, StringComparison.Ordinal)) >= 0)
        {
            splitterCount++;
            idx += "<GridSplitter".Length;
        }

        Assert.True(splitterCount >= 2, $"Expected ≥2 GridSplitters in Drift pane, found {splitterCount}.");
        Assert.Contains("ResizeDirection=\"Rows\"", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.DetailMinHeight}\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DriftFindingsHaveNoMaxHeightCap()
    {
        string slice = DriftSlice();
        Assert.Contains("Drift findings", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"200\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"96\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3Plan13AndDesktopLayoutDocLockLayout03()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-03", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-133 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-03", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("Drift", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutDriftLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string DriftSlice()
    {
        string main = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/Mfc.Desktop/MainWindow.axaml"));
        int start = main.IndexOf("<!-- Drift (no automatic fix) -->", StringComparison.Ordinal);
        Assert.True(start >= 0, "Drift module marker missing.");
        int end = main.IndexOf("<!-- Audit (read-only) -->", start, StringComparison.Ordinal);
        Assert.True(end > start, "Audit marker missing after Drift.");
        return main[start..end];
    }

    private static string FindRepoRoot()
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
