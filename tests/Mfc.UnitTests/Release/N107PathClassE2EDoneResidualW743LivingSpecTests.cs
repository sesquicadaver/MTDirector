using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-43: known-limitations locks intentional N1-07 path-class E2E DONE residual.</summary>
public sealed class N107PathClassE2EDoneResidualW743LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsN107PathClassE2EDoneResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-43 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("N1-07 (#109)", content, StringComparison.Ordinal);
        Assert.Contains("PathClassE2EDriftLivingSpecTests", content, StringComparison.Ordinal);
        Assert.Contains("MVP CLOSED", content, StringComparison.Ordinal);
        Assert.Contains("M6(+N1-07) → MVP CLOSED", content, StringComparison.Ordinal);
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
