using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-26: known-limitations locks intentional MVP scope-lock no-campaigns/auto-deploy residual.</summary>
public sealed class MvpNoCampaignsAutoDeployResidualW726LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsMvpNoCampaignsAutoDeployResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-26 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("No campaigns, auto-deploy, auto-fix drift", content, StringComparison.Ordinal);
        Assert.Contains("web/mobile UI", content, StringComparison.Ordinal);
        Assert.Contains("SIEM/SOAR in Controller", content, StringComparison.Ordinal);
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
