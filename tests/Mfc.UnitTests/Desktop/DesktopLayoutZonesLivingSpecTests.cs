using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-09 / W7-145: Inventory/Zones MaxHeight frames → layout token floors Living Spec.</summary>
public sealed class DesktopLayoutZonesLivingSpecTests
{
    [Fact]
    public void Ac1ZonesListsUseListMinHeightAndNoMaxHeightCaps()
    {
        string slice = ZonesSlice();
        Assert.Contains("Zones.Zones", slice, StringComparison.Ordinal);
        Assert.Contains("Zones.Bindings", slice, StringComparison.Ordinal);
        Assert.Contains("Zones.ResolveResults", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"220\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"240\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ZonesPrimaryListsAndResolveFrameBindListMinHeight()
    {
        string slice = ZonesSlice();
        string[] bindings = ["Zones.Zones", "Zones.Bindings", "Zones.ResolveResults"];
        foreach (string binding in bindings)
        {
            int idx = slice.IndexOf($"ItemsSource=\"{{Binding {binding}}}\"", StringComparison.Ordinal);
            Assert.True(idx >= 0, $"Missing ItemsSource binding {binding}");
            if (binding == "Zones.ResolveResults")
            {
                int borderStart = slice.LastIndexOf("<Border", idx, StringComparison.Ordinal);
                Assert.True(borderStart >= 0, "ResolveResults Border missing.");
                int borderTagEnd = slice.IndexOf('>', borderStart);
                string openTag = slice[borderStart..borderTagEnd];
                Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", openTag, StringComparison.Ordinal);
            }
            else
            {
                int listStart = slice.LastIndexOf("<ListBox", idx, StringComparison.Ordinal);
                Assert.True(listStart >= 0, $"ListBox open missing for {binding}");
                int listTagEnd = slice.IndexOf('>', listStart);
                string openTag = slice[listStart..listTagEnd];
                Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", openTag, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Ac3Plan13AndDesktopLayoutDocLockLayout09()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-09", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-145 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-09", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("Zones", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutZonesLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string ZonesSlice()
    {
        string main = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/Mfc.Desktop/MainWindow.axaml"));
        int start = main.IndexOf("<!-- Inventory detail + Zones -->", StringComparison.Ordinal);
        Assert.True(start >= 0, "Inventory/Zones marker missing.");
        int end = main.IndexOf("<!-- Node -->", start, StringComparison.Ordinal);
        Assert.True(end > start, "Node marker missing after Inventory/Zones.");
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
