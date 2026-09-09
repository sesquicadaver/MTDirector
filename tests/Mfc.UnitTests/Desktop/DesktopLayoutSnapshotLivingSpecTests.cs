using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-01 / W7-129: Snapshot tab single primary pane + splitter Living Spec.</summary>
public sealed class DesktopLayoutSnapshotLivingSpecTests
{
    [Fact]
    public void Ac1SnapshotPanelUsesSinglePrimaryStarAndDetailStarRows()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        int snapshotMarker = main.IndexOf("<!-- Snapshots: Snapshot + Diff -->", StringComparison.Ordinal);
        Assert.True(snapshotMarker >= 0, "Snapshot module marker missing.");
        int semanticDiff = main.IndexOf("<TabItem Header=\"Semantic diff\">", snapshotMarker, StringComparison.Ordinal);
        Assert.True(semanticDiff > snapshotMarker, "Semantic diff tab missing after Snapshot.");
        string snapshotSlice = main[snapshotMarker..semanticDiff];

        Assert.Contains("RowDefinitions=\"Auto,*,Auto,*\"", snapshotSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("RowDefinitions=\"Auto,Auto,*,Auto,*,Auto\"", snapshotSlice, StringComparison.Ordinal);
        Assert.Contains("<GridSplitter", snapshotSlice, StringComparison.Ordinal);
        Assert.Contains("ResizeDirection=\"Rows\"", snapshotSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2SnapshotUsesConfigOrObservationTabsWithoutPrimaryMaxHeight()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        int snapshotMarker = main.IndexOf("<!-- Snapshots: Snapshot + Diff -->", StringComparison.Ordinal);
        int semanticDiff = main.IndexOf("<TabItem Header=\"Semantic diff\">", snapshotMarker, StringComparison.Ordinal);
        string snapshotSlice = main[snapshotMarker..semanticDiff];

        Assert.Contains("<TabItem Header=\"Configuration\">", snapshotSlice, StringComparison.Ordinal);
        Assert.Contains("<TabItem Header=\"Observations\">", snapshotSlice, StringComparison.Ordinal);
        Assert.Contains("Snapshot.ConfigurationRecords", snapshotSlice, StringComparison.Ordinal);
        Assert.Contains("Snapshot.ObservationRecords", snapshotSlice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", snapshotSlice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.DetailMinHeight}\"", snapshotSlice, StringComparison.Ordinal);

        // Primary list boxes must not cap height; detail must not use the old MaxHeight=220 trap.
        Assert.DoesNotContain("MaxHeight=\"220\"", snapshotSlice, StringComparison.Ordinal);
        Assert.DoesNotContain("MinHeight=\"80\"", snapshotSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3Plan13AndDesktopLayoutDocLockLayout01()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-01", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-129 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-01", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("GridSplitter", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutSnapshotLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
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
