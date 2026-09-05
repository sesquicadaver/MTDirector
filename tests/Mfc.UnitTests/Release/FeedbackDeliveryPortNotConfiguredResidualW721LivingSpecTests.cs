using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-21: known-limitations locks intentional IResponseFeedbackDeliveryPort not-configured residual.</summary>
public sealed class FeedbackDeliveryPortNotConfiguredResidualW721LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsFeedbackDeliveryPortNotConfiguredResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-21 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("IResponseFeedbackDeliveryPort", content, StringComparison.Ordinal);
        Assert.Contains("not configured", content, StringComparison.Ordinal);
        Assert.Contains("external analytics complex", content, StringComparison.Ordinal);
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
