using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-23: known-limitations locks intentional SHA256SUMS attestation packaging residual.</summary>
public sealed class Sha256SumsAttestationResidualW723LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsSha256SumsAttestationResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-23 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS", content, StringComparison.Ordinal);
        Assert.Contains("documented attestation", content, StringComparison.Ordinal);
        Assert.Contains("GPG/Sigstore", content, StringComparison.Ordinal);
        Assert.Contains("RELEASE_SIGNING.md", content, StringComparison.Ordinal);
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
