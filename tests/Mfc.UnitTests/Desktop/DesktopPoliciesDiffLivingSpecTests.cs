using System.Collections.ObjectModel;
using System.Reflection;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-DIFF-01 / W7-108: Desktop Policies Diff execute path Living Spec depth.</summary>
public sealed class DesktopPoliciesDiffLivingSpecTests
{
    [Fact]
    public void Ac1WireAndPanelExposeDiffPolicyRevisionsAndDiffAsync()
    {
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.DiffPolicyRevisionsAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.DiffAsync)));
        Assert.NotNull(typeof(PolicyDiffPanelResult).GetProperty(nameof(PolicyDiffPanelResult.RiskLevel)));
        Assert.NotNull(typeof(PolicyDiffPanelResult).GetProperty(nameof(PolicyDiffPanelResult.Rows)));
        Assert.NotNull(typeof(PolicyDiffPanelResult).GetProperty(nameof(PolicyDiffPanelResult.Lines)));
        Assert.NotNull(typeof(PolicyDiffRowListItem).GetProperty(nameof(PolicyDiffRowListItem.KindText)));
        Assert.NotNull(typeof(PolicyDiffRowListItem).GetProperty(nameof(PolicyDiffRowListItem.DetailText)));
        Assert.NotNull(typeof(PolicyDiffRowListItem).GetProperty(nameof(PolicyDiffRowListItem.SummaryLine)));

        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("DiffPolicyRevisionsAsync", panel, StringComparison.Ordinal);
        Assert.Contains("Task<PolicyDiffPanelResult> DiffAsync(", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ViewModelExposesDiffCommandBaselineAndTypedDiffSurface()
    {
        Type vm = typeof(PoliciesViewModel);
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.DiffCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.DiffBaselineCatalogItem)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.DiffBaselineRevisionIdText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.DiffRows)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.DiffLines)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.RiskLevelText)));
        Assert.Equal(typeof(ObservableCollection<PolicyDiffRowListItem>), vm.GetProperty(nameof(PoliciesViewModel.DiffRows))!.PropertyType);
    }

    [Fact]
    public void Ac3DiffCommandGuardsOnCanOperateAndParsesBaselineAfterRevisionIdsInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("[RelayCommand(CanExecute = nameof(CanOperate))]", vm, StringComparison.Ordinal);
        Assert.Contains("private async Task DiffAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("TryParseRevisionId(out Guid afterId)", vm, StringComparison.Ordinal);
        Assert.Contains("Guid.TryParse(DiffBaselineRevisionIdText.Trim(), out Guid beforeId)", vm, StringComparison.Ordinal);
        Assert.Contains("Enter a valid baseline revision UUID for DiffPolicyRevisions.", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.DiffAsync(beforeId, afterId, ct)", vm, StringComparison.Ordinal);
        Assert.Contains("DiffRows.Add(row)", vm, StringComparison.Ordinal);
        Assert.Contains("DiffLines.Add(line)", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4PanelDiffAsyncMapsSemanticPacketRiskRuleFindingKindsWithoutLocalEngine()
    {
        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("KindText = \"semantic\"", panel, StringComparison.Ordinal);
        Assert.Contains("KindText = \"packet-space\"", panel, StringComparison.Ordinal);
        Assert.Contains("KindText = \"risk-driver\"", panel, StringComparison.Ordinal);
        Assert.Contains("KindText = \"rule\"", panel, StringComparison.Ordinal);
        Assert.Contains("KindText = \"finding\"", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", panel, StringComparison.Ordinal);
        Assert.Contains(".DiffPolicyRevisionsAsync(beforeRevisionId, afterRevisionId, cancellationToken)", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5MainWindowBindsPoliciesDiffBaselineCommandRowsAndLines()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Semantic diff", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffBaselineCatalogItem", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffBaselineRevisionIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffRows", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffLines", axaml, StringComparison.Ordinal);
        Assert.Contains("PolicyDiffRowListItem", axaml, StringComparison.Ordinal);
        Assert.Contains("KindText", axaml, StringComparison.Ordinal);
        Assert.Contains("DetailText", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6Plan10MatrixAndPriorDeskPolicy01DiffPresenceRemainPresent()
    {
        string root = FindRepoRoot();
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));
        Assert.Contains("DESK-DIFF-01", plan10, StringComparison.Ordinal);
        Assert.Contains("DiffCommand", plan10, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs")));
        string policy = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs"));
        Assert.Contains("DiffCommand", policy, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffCommand", policy, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/PolicyDesktopServiceTests.cs")));
        string service = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/PolicyDesktopServiceTests.cs"));
        Assert.Contains("DiffAsync", service, StringComparison.Ordinal);
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
