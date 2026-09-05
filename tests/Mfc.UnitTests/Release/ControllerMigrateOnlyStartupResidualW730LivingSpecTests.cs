using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-30: known-limitations locks intentional Controller migrate-only startup residual.</summary>
public sealed class ControllerMigrateOnlyStartupResidualW730LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsControllerMigrateOnlyStartupResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-30 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("Controller does not migrate on normal startup", content, StringComparison.Ordinal);
        Assert.Contains("--migrate-only", content, StringComparison.Ordinal);
        Assert.Contains("EF migrations bundle", content, StringComparison.Ordinal);
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
