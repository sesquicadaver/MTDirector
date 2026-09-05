using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-25: known-limitations locks intentional MVP scope-lock no-NAT/RAW writes residual.</summary>
public sealed class MvpNoNatRawWritesResidualW725LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsMvpNoNatRawWritesResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-25 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("No NAT / RAW / Mangle", content, StringComparison.Ordinal);
        Assert.Contains("VLAN **writes**", content, StringComparison.Ordinal);
        Assert.Contains("managed filter/onboarding/deploy allowlists", content, StringComparison.Ordinal);
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
