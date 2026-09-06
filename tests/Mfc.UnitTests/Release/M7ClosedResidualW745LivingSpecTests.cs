using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-45: known-limitations locks intentional M7.1…M7.4 CLOSED residual.</summary>
public sealed class M7ClosedResidualW745LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsM7ClosedResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-45 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("M7.1…M7.4 (#110–#136)", content, StringComparison.Ordinal);
        Assert.Contains("M7.4 CLOSED", content, StringComparison.Ordinal);
        Assert.Contains("v0.2.0", content, StringComparison.Ordinal);
        Assert.Contains("Post-MVP M7 = **0** open", content, StringComparison.Ordinal);
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
