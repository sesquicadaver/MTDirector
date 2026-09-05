using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>W7-15: pilot-runbook Production mTLS checklist correlates Connect via TraceIdentifier.</summary>
public sealed class PilotMtlsTraceIdentifierW715LivingSpecTests
{
    [Fact]
    public void Ac1PilotRunbookDocumentsTraceIdentifierCorrelation()
    {
        string path = Path.Combine(RepoRoot(), "docs/operations/pilot-runbook.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Production mTLS checklist (W7-09)", content, StringComparison.Ordinal);
        Assert.Contains("Correlate logs (W7-15)", content, StringComparison.Ordinal);
        Assert.Contains("TraceIdentifier=", content, StringComparison.Ordinal);
        Assert.Contains("Information logs", content, StringComparison.Ordinal);
        Assert.Contains("SECURITY.md", content, StringComparison.Ordinal);
        Assert.Contains("full thumbprint", content, StringComparison.OrdinalIgnoreCase);
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
