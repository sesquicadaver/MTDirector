using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-COMPOSE-01 / W7-115: Desktop Policies Compose+RecordAnalysis Living Spec depth.</summary>
public sealed class DesktopPoliciesComposeLivingSpecTests
{
    [Fact]
    public void Ac1WireAndPanelExposeComposeAndRecordAnalysisApis()
    {
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.ComposeEffectivePolicyAsync)));
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.RecordAnalysisRunAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.ComposeAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.RecordAnalysisRunAsync)));
        Assert.NotNull(typeof(PolicyComposePanelResult).GetProperty(nameof(PolicyComposePanelResult.LogicalEffectiveHash)));
        Assert.NotNull(typeof(PolicyComposePanelResult).GetProperty(nameof(PolicyComposePanelResult.Findings)));
        Assert.NotNull(typeof(PolicyAnalysisRunListItem).GetProperty(nameof(PolicyAnalysisRunListItem.AckableFindings)));
    }

    [Fact]
    public void Ac2ViewModelExposesComposeAndRecordAnalysisCommandsAndSurface()
    {
        Type vm = typeof(PoliciesViewModel);
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ComposeCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.RecordAnalysisCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ComposeNodeIdText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.AnalysisRiskLevelText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.AnalysisRunIdText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.Findings)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.SelectedFinding)));
    }

    [Fact]
    public void Ac3ComposeCommandParsesNodeIdAndCallsPanelInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("private async Task ComposeAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("Guid.TryParse(ComposeNodeIdText.Trim(), out Guid nodeId)", vm, StringComparison.Ordinal);
        Assert.Contains("Enter a node UUID for ComposeEffectivePolicy", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.ComposeAsync(nodeId, ct)", vm, StringComparison.Ordinal);
        Assert.Contains("_logicalEffectiveHash = compose.LogicalEffectiveHash", vm, StringComparison.Ordinal);
        Assert.Contains("Findings.Add(finding)", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4RecordAnalysisUsesLogicalHashAndPanelRecordAnalysisRunInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("private async Task RecordAnalysisAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("TryRequireLoadedRevision(out Guid revisionId, out byte[] hash)", vm, StringComparison.Ordinal);
        Assert.Contains("byte[] logical = _logicalEffectiveHash ?? hash", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.RecordAnalysisRunAsync(", vm, StringComparison.Ordinal);
        Assert.Contains("AnalysisRunIdText = run.Id.ToString(\"D\")", vm, StringComparison.Ordinal);
        Assert.Contains("CanExecute = nameof(CanOperate)", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5PanelComposeAndRecordAnalysisDelegateWithoutLocalSemanticEngine()
    {
        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("ComposeEffectivePolicyAsync(nodeId, cancellationToken)", panel, StringComparison.Ordinal);
        Assert.Contains("RecordAnalysisRunAsync(", panel, StringComparison.Ordinal);
        Assert.Contains("DESKTOP_COMPOSE_FINDING", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6MainWindowBindsComposeAndRecordAnalysisAndPlan11MatrixRemainPresent()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Policies.ComposeNodeIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ComposeCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Compose findings\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.RecordAnalysisCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Record analysis\"", axaml, StringComparison.Ordinal);

        string root = FindRepoRoot();
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));
        Assert.Contains("DESK-COMPOSE-01", plan11, StringComparison.Ordinal);
        Assert.Contains("ComposeCommand", plan11, StringComparison.Ordinal);
        Assert.Contains("RecordAnalysisCommand", plan11, StringComparison.Ordinal);
        string policy = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs"));
        Assert.Contains("ComposeCommand", policy, StringComparison.Ordinal);
        Assert.Contains("Policies.ComposeCommand", policy, StringComparison.Ordinal);
        string mvp = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMvpWorkflowsLivingSpecTests.cs"));
        Assert.Contains("RecordAnalysisCommand", mvp, StringComparison.Ordinal);
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
