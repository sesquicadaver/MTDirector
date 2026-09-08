using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-SUBMIT-01 / W7-114: Desktop Policies SubmitForReview Living Spec depth.</summary>
public sealed class DesktopPoliciesSubmitLivingSpecTests
{
    [Fact]
    public void Ac1WireAndPanelExposeSubmitForReviewAsync()
    {
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.SubmitRevisionForReviewAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.SubmitForReviewAsync)));
        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("SubmitForReviewAsync", panel, StringComparison.Ordinal);
        Assert.Contains("SubmitRevisionForReviewAsync", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ViewModelExposesSubmitCommandAndLoadedRevisionSurface()
    {
        Type vm = typeof(PoliciesViewModel);
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.SubmitCommand)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.RevisionIdText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ContentHashText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.ErrorText)));
        Assert.NotNull(vm.GetProperty(nameof(PoliciesViewModel.IsReadOnly)));
    }

    [Fact]
    public void Ac3SubmitCommandGuardsOnCanOperateAndRequiresLoadedRevisionInSource()
    {
        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("[RelayCommand(CanExecute = nameof(CanOperate))]", vm, StringComparison.Ordinal);
        Assert.Contains("private async Task SubmitAsync()", vm, StringComparison.Ordinal);
        Assert.Contains("TryRequireLoadedRevision(out Guid revisionId, out byte[] hash)", vm, StringComparison.Ordinal);
        Assert.Contains("await _policies.SubmitForReviewAsync(revisionId, hash, ct)", vm, StringComparison.Ordinal);
        Assert.Contains("private bool TryRequireLoadedRevision(out Guid revisionId, out byte[] hash)", vm, StringComparison.Ordinal);
        Assert.Contains("private bool CanOperate()", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4PanelSubmitForReviewDelegatesToClientAndReturnsPanelState()
    {
        string panel = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("public async Task<PolicyRevisionPanelState> SubmitForReviewAsync(", panel, StringComparison.Ordinal);
        Assert.Contains("SubmitRevisionForReviewAsync(revisionId, expectedContentHash, cancellationToken)", panel, StringComparison.Ordinal);
        Assert.Contains("return ToPanelState(revision)", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5MainWindowBindsPoliciesSubmitForReviewCommand()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Policies.SubmitCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Submit for review\"", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6Plan11MatrixAndPriorDeskPolicy01SubmitPresenceRemainPresent()
    {
        string root = FindRepoRoot();
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));
        Assert.Contains("DESK-SUBMIT-01", plan11, StringComparison.Ordinal);
        Assert.Contains("SubmitCommand", plan11, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs")));
        string policy = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesLivingSpecTests.cs"));
        Assert.Contains("SubmitCommand", policy, StringComparison.Ordinal);
        Assert.Contains("Policies.SubmitCommand", policy, StringComparison.Ordinal);
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
