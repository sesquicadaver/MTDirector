using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-CATALOG-01 / W7-124: Desktop Policies Catalog refresh Living Spec is present and documented.</summary>
public sealed class CtDeskCatalog01DesktopPoliciesCatalogLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesCatalogLivingSpecAndPlan12MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesCatalogLivingSpecTests.cs");
        string plan12 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-12-desktop-policies-residual-lifecycle.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopPoliciesCatalogLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac2ViewModelExposesRefreshCatalogCommandAndSurface", body, StringComparison.Ordinal);
        Assert.Contains("Ac3RefreshCatalogCallsListCatalogAndApplyCatalogInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac4SelectCatalogItemLoadsLatestRevisionInSource", body, StringComparison.Ordinal);
        Assert.Contains("W7-124 DONE", plan12, StringComparison.Ordinal);
        Assert.Contains("PLAN-12 COMPLETE", plan12, StringComparison.Ordinal);
        Assert.Contains("DESK-CATALOG-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesCatalogLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-124 Living Spec lock)", limitations, StringComparison.Ordinal);
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
