using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-04 / W7-135: Audit event list + payload splitter Living Spec.</summary>
public sealed class DesktopLayoutAuditLivingSpecTests
{
    [Fact]
    public void Ac1AuditUsesListStarSplitterAndPayloadStar()
    {
        string slice = AuditSlice();
        Assert.Contains("RowDefinitions=\"Auto,*,Auto,*\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("IsVisible=\"{Binding IsAuditSelected}\" RowDefinitions=\"Auto,*,*\"", slice, StringComparison.Ordinal);
        Assert.Contains("<GridSplitter", slice, StringComparison.Ordinal);
        Assert.Contains("ResizeDirection=\"Rows\"", slice, StringComparison.Ordinal);
        Assert.Contains("Audit.Events", slice, StringComparison.Ordinal);
        Assert.Contains("Audit.SelectedEvent.PayloadJson", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.DetailMinHeight}\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2AuditPayloadLabelAndNoLegacyStarStarOnlyRows()
    {
        string slice = AuditSlice();
        Assert.Contains("Payload (JSON)", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("IsVisible=\"{Binding IsAuditSelected}\"\n              RowDefinitions=\"Auto,*,*\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("IsVisible=\"{Binding IsAuditSelected}\" RowDefinitions=\"Auto,*,*\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3Plan13AndDesktopLayoutDocLockLayout04()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-04", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-135 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-04", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("Audit", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutAuditLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string AuditSlice()
    {
        string main = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/Mfc.Desktop/MainWindow.axaml"));
        int start = main.IndexOf("<!-- Audit (read-only) -->", StringComparison.Ordinal);
        Assert.True(start >= 0, "Audit module marker missing.");
        int end = main.IndexOf("</Window>", start, StringComparison.Ordinal);
        Assert.True(end > start, "Window end missing after Audit.");
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
