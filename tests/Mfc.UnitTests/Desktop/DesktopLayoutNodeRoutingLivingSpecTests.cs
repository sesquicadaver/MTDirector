using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-06 / W7-139: Node + RoutingAssurance MaxHeight → layout token floors Living Spec.</summary>
public sealed class DesktopLayoutNodeRoutingLivingSpecTests
{
    [Fact]
    public void Ac1NodeAndRoutingListsUseListMinHeightAndNoMaxHeightCaps()
    {
        string slice = NodeSlice();
        Assert.Contains("Node.WorkflowDeviceLines", slice, StringComparison.Ordinal);
        Assert.Contains("RoutingAssurance.ExpectationLines", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"160\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"200\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"280\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2NodeAndRoutingPrimaryListsAllBindListMinHeight()
    {
        string slice = NodeSlice();
        string[] bindings =
        [
            "Node.WorkflowDeviceLines",
            "Node.ZoneSummaryLines",
            "Node.VrrpMembers",
            "Node.VrrpPairFindings",
            "Node.DeviceMembers",
            "Node.DeviceHashLines",
            "RoutingAssurance.ExpectationLines",
            "RoutingAssurance.FindingLines",
            "RoutingAssurance.TraceSummaryLines",
        ];

        foreach (string binding in bindings)
        {
            int idx = slice.IndexOf($"ItemsSource=\"{{Binding {binding}}}\"", StringComparison.Ordinal);
            Assert.True(idx >= 0, $"Missing ItemsSource binding {binding}");
            int listStart = slice.LastIndexOf("<ListBox", idx, StringComparison.Ordinal);
            Assert.True(listStart >= 0, $"ListBox open missing for {binding}");
            int listTagEnd = slice.IndexOf('>', listStart);
            string openTag = slice[listStart..listTagEnd];
            Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", openTag, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac3Plan13AndDesktopLayoutDocLockLayout06()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-06", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-139 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-06", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("RoutingAssurance", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutNodeRoutingLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string NodeSlice()
    {
        string main = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/Mfc.Desktop/MainWindow.axaml"));
        int start = main.IndexOf("<!-- Node -->", StringComparison.Ordinal);
        Assert.True(start >= 0, "Node marker missing.");
        int end = main.IndexOf("<!-- Snapshots: Snapshot + Diff -->", start, StringComparison.Ordinal);
        Assert.True(end > start, "Snapshots marker missing after Node.");
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
