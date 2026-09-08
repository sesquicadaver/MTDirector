using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-DRAFT-01 / W7-122: Desktop Policies Create/Load draft Living Spec depth.</summary>
public sealed class DesktopPoliciesDraftLivingSpecTests
{
    [Fact]
    public void Ac1WireAndPanelExposeCreateDraftAndLoadApis()
    {
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.CreateDraftPolicyAsync)));
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.GetPolicyRevisionAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.CreateDraftAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.LoadRevisionAsync)));
        Assert.NotNull(typeof(PolicyRevisionPanelState).GetProperty(nameof(PolicyRevisionPanelState.RevisionId)));
    }

    [Fact]
    public void Ac2ViewModelExposesCreateDraftLoadCommandsAndSurface()
    {
        Type vm = typeof(PoliciesViewModel);
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.CreateDraftCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.LoadCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.DraftNameText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.RevisionIdText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ErrorText)));
    }

    [Fact]
    public void Ac3CreateDraftRequiresNameAndCallsPanelInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("private async Task CreateDraftAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("Enter a draft policy name.", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.CreateDraftAsync(DraftNameText.Trim(), PolicyKind.CompanyBaseline, ct)", vm, StringComparison.Ordinal);
        Assert.Contains("CanExecute = nameof(CanOperate)", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4LoadParsesRevisionIdAndCallsPanelInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("private async Task LoadAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("TryParseRevisionId(out Guid revisionId)", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.LoadRevisionAsync(revisionId, ct)", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5PanelCreateDraftAndLoadDelegateWithoutLocalSemanticEngine()
    {
        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("CreateDraftPolicyAsync(name, kind, PolicyOwnerScope.Company, ownerId: null, cancellationToken)", panel, StringComparison.Ordinal);
        Assert.Contains("GetPolicyRevisionAsync(revisionId, cancellationToken)", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6MainWindowBindsCreateDraftLoadAndPlan12MatrixRemainPresent()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Policies.DraftNameText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CreateDraftCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Create draft\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.RevisionIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.LoadCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Load\"", axaml, StringComparison.Ordinal);

        string root = FindRepoRoot();
        string plan12 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-12-desktop-policies-residual-lifecycle.md"));
        Assert.Contains("DESK-DRAFT-01", plan12, StringComparison.Ordinal);
        Assert.Contains("CreateDraftCommand", plan12, StringComparison.Ordinal);
        Assert.Contains("LoadCommand", plan12, StringComparison.Ordinal);
        string policy = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs"));
        Assert.Contains("CreateDraftCommand", policy, StringComparison.Ordinal);
        Assert.Contains("Policies.LoadCommand", policy, StringComparison.Ordinal);
        string mvp = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMvpWorkflowsLivingSpecTests.cs"));
        Assert.Contains("Policies.CreateDraftCommand", mvp, StringComparison.Ordinal);
        Assert.Contains("Policies.LoadCommand", mvp, StringComparison.Ordinal);
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
