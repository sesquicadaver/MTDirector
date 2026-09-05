using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-34: known-limitations locks intentional Desktop window does not stop Controller residual.</summary>
public sealed class DesktopWindowDoesNotStopControllerResidualW734LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsDesktopWindowDoesNotStopControllerResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-34 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("Closing the Desktop window **does not** stop Controller", content, StringComparison.Ordinal);
        Assert.Contains("separate OS processes", content, StringComparison.Ordinal);
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
