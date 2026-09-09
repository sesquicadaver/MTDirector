using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-08 / W7-143: Shell chrome column GridSplitter Living Spec.</summary>
public sealed class DesktopLayoutShellChromeLivingSpecTests
{
    [Fact]
    public void Ac1ShellChromeUsesAutoSplitterColumnsBetweenPanes()
    {
        string slice = ShellChromeSlice();
        Assert.Contains("ColumnDefinitions=\"260,Auto,150,Auto,*\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"260,12,150,12,*\"", slice, StringComparison.Ordinal);
        Assert.Contains("ResizeDirection=\"Columns\"", slice, StringComparison.Ordinal);
        Assert.Contains("Inventory.Roots", slice, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Modules}\"", slice, StringComparison.Ordinal);

        int splitterCount = 0;
        int idx = 0;
        while ((idx = slice.IndexOf("<GridSplitter", idx, StringComparison.Ordinal)) >= 0)
        {
            splitterCount++;
            idx += "<GridSplitter".Length;
        }

        Assert.True(splitterCount >= 2, $"Expected ≥2 column GridSplitters in shell chrome, found {splitterCount}.");
    }

    [Fact]
    public void Ac2ShellChromePanesHaveMinWidthFloors()
    {
        string slice = ShellChromeSlice();
        Assert.Contains("MinWidth=\"160\"", slice, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"100\"", slice, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"240\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3Plan13AndDesktopLayoutDocLockLayout08()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-08", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-143 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-08", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("Shell chrome", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutShellChromeLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string ShellChromeSlice()
    {
        string main = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/Mfc.Desktop/MainWindow.axaml"));
        int start = main.IndexOf("<!-- DESK-LAYOUT-08:", StringComparison.Ordinal);
        Assert.True(start >= 0, "DESK-LAYOUT-08 shell chrome marker missing.");
        int end = main.IndexOf("<!-- Inventory detail + Zones -->", start, StringComparison.Ordinal);
        Assert.True(end > start, "Inventory detail marker missing after shell chrome.");
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
