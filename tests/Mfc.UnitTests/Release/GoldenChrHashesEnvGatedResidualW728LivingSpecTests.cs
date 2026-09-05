using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-28: known-limitations locks intentional Golden live CHR hashes env-gated residual.</summary>
public sealed class GoldenChrHashesEnvGatedResidualW728LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsGoldenChrHashesEnvGatedResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-28 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("Golden live CHR hashes", content, StringComparison.Ordinal);
        Assert.Contains("env-gated", content, StringComparison.Ordinal);
        Assert.Contains("isolated runner", content, StringComparison.Ordinal);
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
