using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-41: known-limitations locks intentional RouterOs WriteEnabled operator residual.</summary>
public sealed class RouterOsWriteEnabledOperatorResidualW741LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsRouterOsWriteEnabledOperatorResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-41 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("Mfc:RouterOs:WriteEnabled=true", content, StringComparison.Ordinal);
        Assert.Contains("P2-07…P2-10 DONE", content, StringComparison.Ordinal);
        Assert.Contains("P2-11", content, StringComparison.Ordinal);
        Assert.Contains("pilot-runbook.md", content, StringComparison.Ordinal);
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
