using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-CATALOG-01 / W7-124: Desktop Policies Catalog refresh Living Spec depth.</summary>
public sealed class DesktopPoliciesCatalogLivingSpecTests
{
    [Fact]
    public void Ac1WireAndPanelExposeListPoliciesAndListCatalogApis()
    {
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.ListPoliciesAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.ListCatalogAsync)));
        Assert.NotNull(typeof(PolicyCatalogListItem).GetProperty(nameof(PolicyCatalogListItem.PolicyId)));
        Assert.NotNull(typeof(PolicyCatalogListItem).GetProperty(nameof(PolicyCatalogListItem.LatestRevisionId)));
        Assert.NotNull(typeof(PolicyCatalogListItem).GetProperty(nameof(PolicyCatalogListItem.SummaryLine)));
    }

    [Fact]
    public void Ac2ViewModelExposesRefreshCatalogCommandAndSurface()
    {
        Type vm = typeof(PoliciesViewModel);
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.RefreshCatalogCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.Catalog)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.SelectedCatalogItem)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.HasEmptyCatalog)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.CapturedFilterHintText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ErrorText)));
    }

    [Fact]
    public void Ac3RefreshCatalogCallsListCatalogAndApplyCatalogInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("private async Task RefreshCatalogAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.ListCatalogAsync(ct)", vm, StringComparison.Ordinal);
        Assert.Contains("ApplyCatalog(items)", vm, StringComparison.Ordinal);
        Assert.Contains("CanExecute = nameof(CanOperate)", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4SelectCatalogItemLoadsLatestRevisionInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("private async Task SelectCatalogItemAsync(PolicyCatalogListItem item)", vm, StringComparison.Ordinal);
        Assert.Contains("partial void OnSelectedCatalogItemChanged(PolicyCatalogListItem? value)", vm, StringComparison.Ordinal);
        Assert.Contains("LoadRevisionAsync(item.LatestRevisionId, ct)", vm, StringComparison.Ordinal);
        Assert.Contains("_suppressCatalogSelection", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5PanelListCatalogDelegatesToListPoliciesWithoutLocalSemanticEngine()
    {
        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("ListPoliciesAsync(PolicyKind.Unspecified, cancellationToken)", panel, StringComparison.Ordinal);
        Assert.Contains("ToCatalogItem", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6MainWindowBindsRefreshCatalogAndPlan12MatrixRemainPresent()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Policies.RefreshCatalogCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Refresh catalog\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.Catalog", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SelectedCatalogItem", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.HasEmptyCatalog", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CapturedFilterHintText", axaml, StringComparison.Ordinal);

        string root = FindRepoRoot();
        string plan12 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-12-desktop-policies-residual-lifecycle.md"));
        Assert.Contains("DESK-CATALOG-01", plan12, StringComparison.Ordinal);
        Assert.Contains("RefreshCatalogCommand", plan12, StringComparison.Ordinal);
        string policy = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs"));
        Assert.Contains("RefreshCatalogCommand", policy, StringComparison.Ordinal);
        Assert.Contains("ListPolicies", policy, StringComparison.Ordinal);
        string mvp = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMvpWorkflowsLivingSpecTests.cs"));
        Assert.Contains("Policies.RefreshCatalogCommand", mvp, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

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
