using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-49: known-limitations locks intentional residual Living Spec corpus COMPLETE.</summary>
public sealed class ResidualCorpusCompleteW749LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsResidualCorpusComplete()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-49 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("corpus COMPLETE", content, StringComparison.Ordinal);
        Assert.Contains("fully Living-Spec locked", content, StringComparison.Ordinal);
        Assert.Contains("ops-parallel", content, StringComparison.Ordinal);
        Assert.Contains("not a §3 stop-gate", content, StringComparison.Ordinal);
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
