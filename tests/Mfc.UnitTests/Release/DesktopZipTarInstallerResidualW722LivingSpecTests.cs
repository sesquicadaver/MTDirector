using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-22: known-limitations locks intentional Desktop zip/tar installer packaging residual.</summary>
public sealed class DesktopZipTarInstallerResidualW722LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsDesktopZipTarInstallerResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-22 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("zip/tar publish directory", content, StringComparison.Ordinal);
        Assert.Contains("Avalonia", content, StringComparison.Ordinal);
        Assert.Contains("not MSI/setup.exe", content, StringComparison.Ordinal);
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
