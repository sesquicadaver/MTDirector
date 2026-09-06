using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-39: known-limitations locks intentional RouterOs Enabled default fail-closed residual.</summary>
public sealed class RouterOsEnabledDefaultFailClosedResidualW739LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsRouterOsEnabledDefaultFailClosedResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-39 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("default remains fail-closed", content, StringComparison.Ordinal);
        Assert.Contains("ProbeOnlyRouterOsReadPort", content, StringComparison.Ordinal);
        Assert.Contains("NotConfiguredSnapshotCapturePort", content, StringComparison.Ordinal);
        Assert.Contains("Mfc:RouterOs:Enabled=true", content, StringComparison.Ordinal);
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
