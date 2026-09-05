using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-20: known-limitations locks intentional Feedback delivery / RouterOS capture outside-DB residual.</summary>
public sealed class FeedbackDeliveryOutsideDbResidualW720LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsFeedbackDeliveryOutsideDbResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-20 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("Feedback **delivery**", content, StringComparison.Ordinal);
        Assert.Contains("RouterOS capture", content, StringComparison.Ordinal);
        Assert.Contains("outside the DB boundary", content, StringComparison.Ordinal);
        Assert.Contains("IUnitOfWork", content, StringComparison.Ordinal);
        Assert.Contains("SEC-07…SEC-15 DONE", content, StringComparison.Ordinal);
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
