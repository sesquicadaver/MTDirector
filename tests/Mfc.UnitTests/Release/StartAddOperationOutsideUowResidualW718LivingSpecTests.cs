using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-18: known-limitations locks intentional Start* AddOperationAsync outside-UoW residual.</summary>
public sealed class StartAddOperationOutsideUowResidualW718LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsStartAddOperationOutsideUowResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-18 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("Start* pre-runtime", content, StringComparison.Ordinal);
        Assert.Contains("AddOperationAsync", content, StringComparison.Ordinal);
        Assert.Contains("stays outside UoW", content, StringComparison.Ordinal);
        Assert.Contains("IUnitOfWork", content, StringComparison.Ordinal);
        Assert.Contains("SEC-07…SEC-15 DONE", content, StringComparison.Ordinal);
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
