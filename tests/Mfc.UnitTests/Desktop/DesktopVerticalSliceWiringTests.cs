using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>M1-30 AC#9: Desktop wires inventory, snapshot viewer, and semantic diff (no Avalonia headless).</summary>
public sealed class DesktopVerticalSliceWiringTests
{
    [Fact]
    public void ShellExposesInventorySnapshotDiffZonesPoliciesOnboardingDeploymentDriftAndAudit()
    {
        System.Reflection.PropertyInfo[] properties = typeof(ShellViewModel).GetProperties();
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Inventory)
                                         && p.PropertyType == typeof(InventoryTreeViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.AddRouter)
                                         && p.PropertyType == typeof(AddRouterWizardViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Node)
                                         && p.PropertyType == typeof(NodeDetailViewModel));
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
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Deployment)
                                         && p.PropertyType == typeof(DeploymentViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Drift)
                                         && p.PropertyType == typeof(DriftViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.Audit)
                                         && p.PropertyType == typeof(AuditViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.RoutingAssurance)
                                         && p.PropertyType == typeof(RoutingAssuranceViewModel));
        Assert.Contains(properties, p => p.Name == nameof(ShellViewModel.SelectedModule)
                                         && p.PropertyType == typeof(ShellNavigationModule));
    }

    [Fact]
    public void DesktopClientsCoverInventorySnapshotCompareZonePolicyOnboardingDeploymentDriftAndAuditRpcs()
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
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.CreateSiteAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.CreateNodeAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.RegisterDeviceAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.UpdateDeviceConnectionAsync)));

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

        Type deployment = typeof(IDeploymentServiceClient);
        Assert.NotNull(deployment.GetMethod(nameof(IDeploymentServiceClient.CreatePlanAsync)));
        Assert.NotNull(deployment.GetMethod(nameof(IDeploymentServiceClient.StartAsync)));
        Assert.NotNull(deployment.GetMethod(nameof(IDeploymentServiceClient.WatchAsync)));
        Assert.NotNull(deployment.GetMethod(nameof(IDeploymentServiceClient.RollbackAsync)));
        Assert.NotNull(deployment.GetMethod(nameof(IDeploymentServiceClient.GetRecoveryStatusAsync)));

        Type drift = typeof(IDriftServiceClient);
        Assert.NotNull(drift.GetMethod(nameof(IDriftServiceClient.ListDeviceDriftEventsAsync)));
        Assert.NotNull(drift.GetMethod(nameof(IDriftServiceClient.GetDriftEventAsync)));

        Type audit = typeof(IAuditServiceClient);
        Assert.NotNull(audit.GetMethod(nameof(IAuditServiceClient.ListAuditEventsAsync)));

        Type routingAssurance = typeof(IRoutingAssuranceServiceClient);
        Assert.NotNull(routingAssurance.GetMethod(nameof(IRoutingAssuranceServiceClient.GetDeviceRoutingAssuranceStateAsync)));
    }
}
