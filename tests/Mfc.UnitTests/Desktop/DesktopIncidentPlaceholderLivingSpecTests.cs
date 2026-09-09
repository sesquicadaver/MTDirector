using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-PLACEHOLDER-01 / W7-150: Incident TextBoxes use PlaceholderText (no obsolete Watermark).</summary>
public sealed class DesktopIncidentPlaceholderLivingSpecTests
{
    [Fact]
    public void Ac1IncidentTextBoxesUsePlaceholderTextNotWatermark()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.DoesNotContain("Watermark=", main, StringComparison.Ordinal);
        Assert.Contains("Incident.SourceEventId", main, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"siem-evt-…\"", main, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"brute_force_login\"", main, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"dedup:…\"", main, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"50\"", main, StringComparison.Ordinal);
        Assert.Contains("Incident.EndpointIdText", main, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx\"", main, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"10.0.0.8\"", main, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"198.51.100.10\"", main, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"tcp\"", main, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2Plan14AndTestingDocLockPlaceholder01()
    {
        string root = FindRepoRoot();
        string plan14 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-14-desktop-avalonia-placeholder-incident-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-PLACEHOLDER-01", plan14, StringComparison.Ordinal);
        Assert.Contains("W7-150 DONE", plan14, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentPlaceholderLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-150 Living Spec lock)", limitations, StringComparison.Ordinal);
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
