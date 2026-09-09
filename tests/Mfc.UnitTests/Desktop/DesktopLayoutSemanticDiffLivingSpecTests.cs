using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-02 / W7-131: Semantic Diff entry list + splitter Living Spec.</summary>
public sealed class DesktopLayoutSemanticDiffLivingSpecTests
{
    [Fact]
    public void Ac1SemanticDiffUsesEntryStarSplitterAndDetailStar()
    {
        string slice = SemanticDiffSlice();
        Assert.Contains("RowDefinitions=\"*,Auto,*\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("RowDefinitions=\"*,8,Auto\"", slice, StringComparison.Ordinal);
        Assert.Contains("<GridSplitter", slice, StringComparison.Ordinal);
        Assert.Contains("ResizeDirection=\"Rows\"", slice, StringComparison.Ordinal);
        Assert.Contains("Diff.VisibleEntries", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.DetailMinHeight}\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2SemanticDiffBeforeAfterDetailHasNoMaxHeightCap()
    {
        string slice = SemanticDiffSlice();
        Assert.Contains("Before record", slice, StringComparison.Ordinal);
        Assert.Contains("After record", slice, StringComparison.Ordinal);
        Assert.Contains("Diff.SelectedEntry.BeforeRecordFields", slice, StringComparison.Ordinal);
        Assert.Contains("Diff.SelectedEntry.AfterRecordFields", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"220\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3Plan13AndDesktopLayoutDocLockLayout02()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-02", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-131 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-02", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("Semantic Diff", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutSemanticDiffLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string SemanticDiffSlice()
    {
        string main = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/Mfc.Desktop/MainWindow.axaml"));
        int start = main.IndexOf("<TabItem Header=\"Semantic diff\">", StringComparison.Ordinal);
        Assert.True(start >= 0, "Semantic diff tab missing.");
        int end = main.IndexOf("<!-- Policies -->", start, StringComparison.Ordinal);
        Assert.True(end > start, "Policies marker missing after Semantic diff.");
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
