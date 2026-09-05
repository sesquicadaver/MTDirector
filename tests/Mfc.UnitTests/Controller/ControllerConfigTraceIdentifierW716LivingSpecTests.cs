using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>W7-16: controller-configuration.md cross-links TraceIdentifier mTLS correlation.</summary>
public sealed class ControllerConfigTraceIdentifierW716LivingSpecTests
{
    [Fact]
    public void Ac1ControllerConfigurationDocumentsTraceIdentifierCorrelation()
    {
        string path = Path.Combine(RepoRoot(), "docs/operations/controller-configuration.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Desktop mTLS client certificate", content, StringComparison.Ordinal);
        Assert.Contains("TraceIdentifier=", content, StringComparison.Ordinal);
        Assert.Contains("SECURITY.md", content, StringComparison.Ordinal);
        Assert.Contains("pilot-runbook.md", content, StringComparison.Ordinal);
        Assert.Contains("Information log", content, StringComparison.Ordinal);
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
