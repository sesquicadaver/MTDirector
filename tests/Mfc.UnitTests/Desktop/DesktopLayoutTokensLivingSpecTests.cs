using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-00 / W7-127: shared Desktop layout density tokens + first MainWindow consumer.</summary>
public sealed class DesktopLayoutTokensLivingSpecTests
{
    [Fact]
    public void Ac1AppAxamlDefinesSharedLayoutHeightTokens()
    {
        string app = ReadSource("src/Mfc.Desktop/App.axaml");
        Assert.Contains("x:Key=\"Mfc.ListMinHeight\"", app, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"Mfc.DetailMinHeight\"", app, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"Mfc.SectionSpacing\"", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2MainWindowBindsTokensOnPrimarySnapshotListAndDetail()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Snapshot.ConfigurationRecords", main, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", main, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.DetailMinHeight}\"", main, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3DesktopLayoutDocAndPlan13MatrixRemainPresent()
    {
        string root = FindRepoRoot();
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("Mfc.ListMinHeight", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("Mfc.DetailMinHeight", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-00", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-00", plan13, StringComparison.Ordinal);
        Assert.Contains("Mfc.ListMinHeight", plan13, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutTokensLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        string root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, relativePath));
    }

    private static string FindRepoRoot()
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
