using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-27: known-limitations locks intentional Live CHR matrix OFF residual.</summary>
public sealed class LiveChrMatrixOffResidualW727LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsLiveChrMatrixOffResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-27 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("Live CHR matrix is **OFF**", content, StringComparison.Ordinal);
        Assert.Contains("Scripted E2E Living Specs", content, StringComparison.Ordinal);
        Assert.Contains("DoD substitute", content, StringComparison.Ordinal);
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
