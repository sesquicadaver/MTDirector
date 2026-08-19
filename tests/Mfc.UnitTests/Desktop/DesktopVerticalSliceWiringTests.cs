using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>M1-30 AC#9: Desktop wires inventory, snapshot viewer, and semantic diff (no Avalonia headless).</summary>
public sealed class DesktopVerticalSliceWiringTests
{
    [Fact]
    public void ShellExposesInventorySnapshotDiffZonesPoliciesAndOnboardingViewModels()
    {
        System.Reflection.PropertyInfo[] properties = typeof(ShellViewModel).GetProperties();
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Inventory)
                                         && p.PropertyType == typeof(InventoryTreeViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Snapshot)
                                         && p.PropertyType == typeof(SnapshotViewerViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Diff)
                                         && p.PropertyType == typeof(SnapshotDiffViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Zones)
                                         && p.PropertyType == typeof(ZonesViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Policies)
                                         && p.PropertyType == typeof(PoliciesViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Onboarding)
                                         && p.PropertyType == typeof(OnboardingViewModel));
    }

    [Fact]
    public void DesktopClientsCoverInventorySnapshotCompareZoneAndPolicyRpcs()
    {
        Type client = typeof(ISnapshotViewerClient);
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.ListCapturesAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.GetSummaryAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.GetAllSectionRecordsAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.CompareSnapshotsAsync)));

        Type inventory = typeof(IInventoryTreeClient);
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.ListAllSitesAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.ListAllNodesAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.GetNodeAsync)));

        Type zones = typeof(IZoneServiceClient);
        Assert.NotNull(zones.GetMethod(nameof(IZoneServiceClient.ListZoneDefinitionsAsync)));
        Assert.NotNull(zones.GetMethod(nameof(IZoneServiceClient.UpsertNodeZoneBindingAsync)));
        Assert.NotNull(zones.GetMethod(nameof(IZoneServiceClient.ResolveZonesForNodeAsync)));

        Type policies = typeof(IPolicyServiceClient);
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.ListRulesAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.GetPolicyRevisionAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.CreateDraftPolicyAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.ValidateRevisionAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.UpsertAddressObjectAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.UpsertServiceObjectAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.ReplaceChainContractsAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.ReplacePolicyTestsAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.DiffPolicyRevisionsAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.ComposeEffectivePolicyAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.SubmitRevisionForReviewAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.RecordAnalysisRunAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.ApproveRevisionAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.ActivateDesiredBindingAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.ReorderRulesAsync)));
        Assert.NotNull(policies.GetMethod(nameof(IPolicyServiceClient.AddRuleAsync)));

        Type onboarding = typeof(IOnboardingServiceClient);
        Assert.NotNull(onboarding.GetMethod(nameof(IOnboardingServiceClient.ValidatePrerequisitesAsync)));
        Assert.NotNull(onboarding.GetMethod(nameof(IOnboardingServiceClient.CreatePlanAsync)));
        Assert.NotNull(onboarding.GetMethod(nameof(IOnboardingServiceClient.StartAsync)));
        Assert.NotNull(onboarding.GetMethod(nameof(IOnboardingServiceClient.WatchAsync)));
        Assert.NotNull(onboarding.GetMethod(nameof(IOnboardingServiceClient.RollbackAsync)));
        Assert.NotNull(onboarding.GetMethod(nameof(IOnboardingServiceClient.GetRecoveryStatusAsync)));
    }
}
