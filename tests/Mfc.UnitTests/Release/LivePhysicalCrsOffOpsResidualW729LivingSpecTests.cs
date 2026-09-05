using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-29: known-limitations locks intentional Live physical CRS hardware OFF ops residual.</summary>
public sealed class LivePhysicalCrsOffOpsResidualW729LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsLivePhysicalCrsOffOpsResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-29 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("Live physical CRS hardware exercise is **OFF**", content, StringComparison.Ordinal);
        Assert.Contains("VrrpCrsE2ELivingSpecTests", content, StringComparison.Ordinal);
        Assert.Contains("ops residual", content, StringComparison.Ordinal);
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
