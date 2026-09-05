using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-24: known-limitations locks intentional CycloneDX-lite SBOM packaging residual.</summary>
public sealed class CycloneDxLiteSbomResidualW724LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsCycloneDxLiteSbomResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-24 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("CycloneDX CLI is optional", content, StringComparison.Ordinal);
        Assert.Contains("CycloneDX-lite", content, StringComparison.Ordinal);
        Assert.Contains("package inventory", content, StringComparison.Ordinal);
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
