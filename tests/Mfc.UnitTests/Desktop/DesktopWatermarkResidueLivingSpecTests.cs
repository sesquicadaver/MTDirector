using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-PLACEHOLDER-02 / W7-152: no obsolete Watermark= under Desktop XAML; PLAN-14 COMPLETE.</summary>
public sealed class DesktopWatermarkResidueLivingSpecTests
{
    [Fact]
    public void Ac1NoWatermarkAttributeInDesktopAxamlFiles()
    {
        string desktop = Path.Combine(FindRepoRoot(), "src/Mfc.Desktop");
        string[] axaml = Directory.GetFiles(desktop, "*.axaml", SearchOption.AllDirectories);
        Assert.NotEmpty(axaml);
        foreach (string path in axaml)
        {
            string text = File.ReadAllText(path);
            Assert.DoesNotContain("Watermark=", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2Plan14CompleteAndDocsLockPlaceholder02()
    {
        string root = FindRepoRoot();
        string plan14 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-14-desktop-avalonia-placeholder-incident-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("PLAN-14 COMPLETE", plan14, StringComparison.Ordinal);
        Assert.Contains("DESK-PLACEHOLDER-02", plan14, StringComparison.Ordinal);
        Assert.Contains("W7-152 DONE", plan14, StringComparison.Ordinal);
        Assert.Contains("DesktopWatermarkResidueLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-152 Living Spec lock)", limitations, StringComparison.Ordinal);
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
