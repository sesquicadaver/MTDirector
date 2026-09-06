using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-35: known-limitations locks intentional Inventory Add router wizard DONE residual.</summary>
public sealed class InventoryAddRouterWizardDoneResidualW735LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsDocumentsInventoryAddRouterWizardDoneResidual()
    {
        string path = Path.Combine(RepoRoot(), "docs/release/known-limitations.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Intentional residual (W7-35 Living Spec lock)", content, StringComparison.Ordinal);
        Assert.Contains("Inventory **Add router** wizard is **DONE**", content, StringComparison.Ordinal);
        Assert.Contains("#309", content, StringComparison.Ordinal);
        Assert.Contains("UpdateDeviceConnection", content, StringComparison.Ordinal);
        Assert.Contains("Site→Node→Device", content, StringComparison.Ordinal);
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
