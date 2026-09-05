using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>W7-14: SECURITY.md documents TraceIdentifier on mTLS principal-map Information logs.</summary>
public sealed class SecurityMtlsTraceIdentifierW714LivingSpecTests
{
    [Fact]
    public void Ac1SecurityMdDocumentsTraceIdentifierOnMtlsMapLog()
    {
        string path = Path.Combine(RepoRoot(), "SECURITY.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("W7-13 / W7-14", content, StringComparison.Ordinal);
        Assert.Contains("Information", content, StringComparison.Ordinal);
        Assert.Contains("HttpContext.TraceIdentifier", content, StringComparison.Ordinal);
        Assert.Contains("TraceIdentifier=", content, StringComparison.Ordinal);
        Assert.Contains("no PEM/full thumbprint", content, StringComparison.Ordinal);
        Assert.Contains("Desktop Connect", content, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SECURITY.md"))
                && File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
