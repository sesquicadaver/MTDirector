using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-17: known-limitations locks intentional resolve-only zone UoW residual.</summary>
public sealed class ResolveOnlyZoneUowResidualW717LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsResolveOnlyZoneUowResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-17 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("resolve-only zone updates", content, StringComparison.Ordinal);
        Assert.Contains("no idempotency/audit triple", content, StringComparison.Ordinal);
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
