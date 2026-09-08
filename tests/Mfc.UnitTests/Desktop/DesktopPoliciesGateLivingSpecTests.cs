using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-GATE-01 / W7-116: Desktop Policies Approve/Bind/Compile Living Spec depth.</summary>
public sealed class DesktopPoliciesGateLivingSpecTests
{
    [Fact]
    public void Ac1WireAndPanelExposeApproveBindAndCompileApis()
    {
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.ApproveRevisionAsync)));
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.ActivateDesiredBindingAsync)));
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.CompileNodeFilterArtifactsAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.ApproveAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.BindAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.CompileNodeFilterArtifactsAsync)));
        Assert.NotNull(typeof(PolicyCompilePanelResult).GetProperty(nameof(PolicyCompilePanelResult.LogicalEffectiveHashHex)));
        Assert.NotNull(typeof(PolicyCompilePanelResult).GetProperty(nameof(PolicyCompilePanelResult.ArtifactLines)));
    }

    [Fact]
    public void Ac2ViewModelExposesApproveBindCompileCommandsAndSurface()
    {
        Type vm = typeof(PoliciesViewModel);
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ApproveCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.BindCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.CompileCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.CompileCapabilityHashText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.CompileArtifactLines)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.AnalysisRunIdText)));
    }

    [Fact]
    public void Ac3ApproveCommandRequiresAnalysisRunAndCallsPanelInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("private async Task ApproveAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("TryRequireLoadedRevision(out Guid revisionId, out byte[] hash)", vm, StringComparison.Ordinal);
        Assert.Contains("Record an analysis run before Approve", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.ApproveAsync(revisionId, runId, hash, _analysisBundleHash, _dependencyFingerprint, ct)", vm, StringComparison.Ordinal);
        Assert.Contains("CanExecute = nameof(CanOperate)", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4BindAndCompileCommandsGuardAndCallPanelInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("private async Task BindAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("Record/Approve analysis run before Bind", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.BindAsync(revisionId, runId, hash, _dependencyFingerprint, ct)", vm, StringComparison.Ordinal);
        Assert.Contains("private async Task CompileAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("Select a Node (or enter its UUID) before CompileNodeFilterArtifacts", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.CompileNodeFilterArtifactsAsync(", vm, StringComparison.Ordinal);
        Assert.Contains("CompileCapabilityHashText", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5PanelApproveBindCompileDelegateToClientWithoutLocalSemanticEngine()
    {
        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("ApproveRevisionAsync(", panel, StringComparison.Ordinal);
        Assert.Contains("ActivateDesiredBindingAsync(", panel, StringComparison.Ordinal);
        Assert.Contains("CompileNodeFilterArtifactsAsync(", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6MainWindowBindsGateCommandsAndPlan11MatrixRemainPresent()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Policies.ApproveCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Approve\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.BindCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Bind\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CompileCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Compile artifacts\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CompileCapabilityHashText", axaml, StringComparison.Ordinal);

        string root = FindRepoRoot();
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));
        Assert.Contains("DESK-GATE-01", plan11, StringComparison.Ordinal);
        Assert.Contains("ApproveCommand", plan11, StringComparison.Ordinal);
        Assert.Contains("BindCommand", plan11, StringComparison.Ordinal);
        Assert.Contains("CompileCommand", plan11, StringComparison.Ordinal);
        string policy = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs"));
        Assert.Contains("ApproveCommand", policy, StringComparison.Ordinal);
        Assert.Contains("BindCommand", policy, StringComparison.Ordinal);
        string mvp = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMvpWorkflowsLivingSpecTests.cs"));
        Assert.Contains("CompileCommand", mvp, StringComparison.Ordinal);
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
