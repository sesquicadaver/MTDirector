using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-ACK-01 / W7-120: Desktop Policies AcknowledgeWarning Living Spec depth.</summary>
public sealed class DesktopPoliciesAcknowledgeLivingSpecTests
{
    [Fact]
    public void Ac1WireAndPanelExposeAcknowledgeWarningApis()
    {
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.AcknowledgeWarningAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.AcknowledgeWarningAsync)));
        Assert.NotNull(typeof(PolicyAnalysisRunListItem).GetProperty(nameof(PolicyAnalysisRunListItem.RiskLevel)));
        Assert.NotNull(typeof(PolicyAnalysisRunListItem).GetProperty(nameof(PolicyAnalysisRunListItem.EffectiveRiskLevel)));
        Assert.NotNull(typeof(PolicyFindingListItem).GetProperty(nameof(PolicyFindingListItem.WarningHash)));
    }

    [Fact]
    public void Ac2ViewModelExposesAcknowledgeWarningCommandAndSurface()
    {
        Type vm = typeof(PoliciesViewModel);
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.AcknowledgeWarningCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.Findings)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.SelectedFinding)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.AnalysisRunIdText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.RiskLevelText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.EffectiveRiskLevelText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ErrorText)));
    }

    [Fact]
    public void Ac3AcknowledgeWarningGuardsOnRunAndFindingAndCallsPanelInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("private async Task AcknowledgeWarningAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("Record an analysis run before Acknowledge warning.", vm, StringComparison.Ordinal);
        Assert.Contains("Select a recorded finding with a warning hash.", vm, StringComparison.Ordinal);
        Assert.Contains("SelectedFinding.WarningHash is not { Length: 32 } warningHash", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.AcknowledgeWarningAsync(runId, warningHash, ct)", vm, StringComparison.Ordinal);
        Assert.Contains("RiskLevelText = run.RiskLevel", vm, StringComparison.Ordinal);
        Assert.Contains("EffectiveRiskLevelText = run.EffectiveRiskLevel", vm, StringComparison.Ordinal);
        Assert.Contains("CanExecute = nameof(CanOperate)", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4PanelAcknowledgeWarningDelegatesToClientWithoutLocalSemanticEngine()
    {
        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("public async Task<PolicyAnalysisRunListItem> AcknowledgeWarningAsync(", panel, StringComparison.Ordinal);
        Assert.Contains("AcknowledgeWarningAsync(analysisRunId, warningHash, cancellationToken)", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5MainWindowBindsAcknowledgeWarningCommand()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Policies.AcknowledgeWarningCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Acknowledge warning\"", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6Plan12MatrixAndPriorDeskPolicy01AckPresenceRemainPresent()
    {
        string root = FindRepoRoot();
        string plan12 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-12-desktop-policies-residual-lifecycle.md"));
        Assert.Contains("DESK-ACK-01", plan12, StringComparison.Ordinal);
        Assert.Contains("AcknowledgeWarningCommand", plan12, StringComparison.Ordinal);
        Assert.Contains("AcknowledgeWarningAsync", plan12, StringComparison.Ordinal);
        string policy = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs"));
        Assert.Contains("AcknowledgeWarning", policy, StringComparison.Ordinal);
        string mvp = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMvpWorkflowsLivingSpecTests.cs"));
        Assert.Contains("AcknowledgeWarningCommand", mvp, StringComparison.Ordinal);
        Assert.Contains("Policies.AcknowledgeWarningCommand", mvp, StringComparison.Ordinal);
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
