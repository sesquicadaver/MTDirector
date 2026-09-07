using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// QG-SIGN-01: release signing residual stays documented (cleartext SHA256SUMS + optional
/// GPG/Sigstore CI gate). Does not enable production cryptographic signing by default.
/// </summary>
public sealed class QgSign01ReleaseSigningLivingSpecTests
{
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

    [Fact]
    public void Ac1ReleaseSigningDocumentsMvpCleartextSha256SumsAttestation()
    {
        string signing = File.ReadAllText(Path.Combine(RepoRoot(), "docs/release/RELEASE_SIGNING.md"));

        Assert.Contains("SHA256SUMS", signing, StringComparison.Ordinal);
        Assert.Contains("cleartext", signing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("generate-sbom-and-checksums.sh", signing, StringComparison.Ordinal);
        Assert.Contains("MFC_RELEASE_GPG_KEY_ID", signing, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2CiSigningGateDocumentedAsFutureNotDefaultEnabled()
    {
        string signing = File.ReadAllText(Path.Combine(RepoRoot(), "docs/release/RELEASE_SIGNING.md"));

        Assert.Contains("CI signing gate", signing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GPG or Sigstore", signing, StringComparison.Ordinal);
        Assert.Contains("future", signing, StringComparison.OrdinalIgnoreCase);
        // Fail closed: must not claim production crypto signing is already the default path.
        Assert.DoesNotContain("cryptographic signing is enabled by default", signing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("production signing required for MVP", signing, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac3ReleaseGatesChecklistReferencesSigningPolicy()
    {
        string gates = File.ReadAllText(Path.Combine(RepoRoot(), "docs/release/release-gates.md"));

        Assert.Contains("RELEASE_SIGNING.md", gates, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS", gates, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4KnownLimitationsDocumentsSigningResidual()
    {
        string limitations = File.ReadAllText(Path.Combine(RepoRoot(), "docs/release/known-limitations.md"));

        Assert.Contains("SHA256SUMS", limitations, StringComparison.Ordinal);
        Assert.Contains("RELEASE_SIGNING.md", limitations, StringComparison.Ordinal);
        Assert.Contains("GPG/Sigstore", limitations, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5DocsMatrixDocumentsQgSign01()
    {
        string root = RepoRoot();
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string plan03 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-03-quality-gates.md"));
        string checklist = File.ReadAllText(Path.Combine(root, "docs/development/signing-gate.md"));

        Assert.Contains("QG-SIGN-01", testing, StringComparison.Ordinal);
        Assert.Contains("Living Specification — QG-SIGN-01", testing, StringComparison.Ordinal);
        Assert.Contains("QgSign01ReleaseSigningLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("W7-56 DONE", plan03, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-01", checklist, StringComparison.Ordinal);
    }
}
