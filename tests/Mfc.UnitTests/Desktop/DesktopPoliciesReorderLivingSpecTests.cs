using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-REORDER-01 / W7-110: Desktop Policies Move up/down Living Spec depth.</summary>
public sealed class DesktopPoliciesReorderLivingSpecTests
{
    [Fact]
    public void Ac1PanelExposesReorderRulesInStageAsync()
    {
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.ReorderRulesInStageAsync)));
        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("ReorderRulesInStageAsync", panel, StringComparison.Ordinal);
        Assert.Contains("ReorderRulesAsync", panel, StringComparison.Ordinal);
        Assert.Contains("contiguous permutation", panel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac2ViewModelExposesMoveUpDownAndReorderCommands()
    {
        Type vm = typeof(PoliciesViewModel);
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.MoveRuleUpCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.MoveRuleDownCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ReorderRulesCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.SelectedRule)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ReorderRuleIdsText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.Rules)));
    }

    [Fact]
    public void Ac3MoveCommandsGuardOnCanMoveSelectedRuleAndCallPanelReorderInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("[RelayCommand(CanExecute = nameof(CanMoveSelectedRule))]", vm, StringComparison.Ordinal);
        Assert.Contains("private Task MoveRuleUpAsync() => MoveSelectedRuleAsync(delta: -1);", vm, StringComparison.Ordinal);
        Assert.Contains("private Task MoveRuleDownAsync() => MoveSelectedRuleAsync(delta: 1);", vm, StringComparison.Ordinal);
        Assert.Contains("private bool CanMoveSelectedRule() => CanMutate() && SelectedRule is not null;", vm, StringComparison.Ordinal);
        Assert.Contains("private async Task MoveSelectedRuleAsync(int delta)", vm, StringComparison.Ordinal);
        Assert.Contains("Select a rule to reorder.", vm, StringComparison.Ordinal);
        Assert.Contains("Rule is already first in its family/chain/stage.", vm, StringComparison.Ordinal);
        Assert.Contains("Rule is already last in its family/chain/stage.", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.ReorderRulesInStageAsync(", vm, StringComparison.Ordinal);
        Assert.Contains("W6-09", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4ReorderRulesCommandParsesUuidListAndCallsPanelInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("[RelayCommand(CanExecute = nameof(CanMutate))]", vm, StringComparison.Ordinal);
        Assert.Contains("private async Task ReorderRulesAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("ReorderRuleIdsText.Split(", vm, StringComparison.Ordinal);
        Assert.Contains("Enter comma-separated rule UUIDs for the same family/chain/stage.", vm, StringComparison.Ordinal);
        Assert.Contains("Invalid rule UUID in reorder list:", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5MainWindowBindsMoveUpDownAndSelectedRule()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Policies.MoveRuleUpCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.MoveRuleDownCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Move up\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Move down\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SelectedRule", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6Plan10MatrixAndPriorDeskPolicy01ReorderPresenceRemainPresent()
    {
        string root = FindRepoRoot();
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));
        Assert.Contains("DESK-REORDER-01", plan10, StringComparison.Ordinal);
        Assert.Contains("MoveRuleUpCommand", plan10, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan10, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMvpWorkflowsLivingSpecTests.cs")));
        string mvp = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMvpWorkflowsLivingSpecTests.cs"));
        Assert.Contains("MoveRuleUpCommand", mvp, StringComparison.Ordinal);
        Assert.Contains("MoveRuleDownCommand", mvp, StringComparison.Ordinal);
        Assert.Contains("ReorderRulesInStageAsync", mvp, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/PoliciesViewModelTests.cs")));
        string vmTests = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/PoliciesViewModelTests.cs"));
        Assert.Contains("MoveRuleDownBuildsStageOrderWithoutUuidPaste", vmTests, StringComparison.Ordinal);
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
