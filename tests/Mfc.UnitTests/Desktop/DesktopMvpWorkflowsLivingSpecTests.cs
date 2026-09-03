using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>Living Spec — Desktop MVP workflows (M6-04) AC 1–12.</summary>
public sealed class DesktopMvpWorkflowsLivingSpecTests
{
    [Fact]
    public void Ac1SingleNavigationModelExposesExactlySevenModules()
    {
        string[] expected =
        [
            nameof(ShellNavigationModule.Inventory),
            nameof(ShellNavigationModule.Node),
            nameof(ShellNavigationModule.Snapshots),
            nameof(ShellNavigationModule.Policies),
            nameof(ShellNavigationModule.Operations),
            nameof(ShellNavigationModule.Drift),
            nameof(ShellNavigationModule.Audit),
        ];
        string[] actual = Enum.GetNames<ShellNavigationModule>();
        Assert.Equal(expected, actual);

        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.SelectedModule)));
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.Modules)));
        PropertyInfo modules = typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.Modules))!;
        Assert.Equal(typeof(IReadOnlyList<ShellNavigationModule>), modules.PropertyType);
    }

    [Fact]
    public void Ac2InventorySurfacesWorkflowStatusVisibly()
    {
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.WorkflowStatusText)));
        string axaml = ReadMainWindowAxaml();
        Assert.Contains("WorkflowStatusText", axaml, StringComparison.Ordinal);
        Assert.Contains("Workflow status", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.SelectedNode.WorkflowStatusText", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2bInventoryAddRouterWizardCoversCreateRegisterConnectionPath()
    {
        Type wizard = typeof(AddRouterWizardViewModel);
        Assert.NotNull(wizard.GetProperty("SubmitCommand"));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.UseExistingSite)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.UseExistingNode)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.DeviceDisplayName)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.ManagementHost)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.Username)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.Password)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.SelectedTrustMode)));
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.AddRouter)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Add router", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.SubmitCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.UseExistingSite", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.ManagementHost", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.Password", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.LoadNeighborsCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.ApplyNeighborCandidateCommand", axaml, StringComparison.Ordinal);

        Type inventory = typeof(IInventoryTreeClient);
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.CreateSiteAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.CreateNodeAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.RegisterDeviceAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.UpdateDeviceConnectionAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.ListNeighborCandidatesAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.ValidateVrrpPairConsistencyAsync)));
    }

    /// <summary>W3.2: Inventory/Add router Probe calls ValidateDeviceConnection (read-only Controller probe).</summary>
    [Fact]
    public void Ac2cInventoryAndAddRouterProbeValidateDeviceConnection()
    {
        Type wizard = typeof(AddRouterWizardViewModel);
        Assert.NotNull(wizard.GetProperty("ProbeCommand"));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.ProbeResultText)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.HasProbeResult)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.CanProbeVisible)));

        Type inventory = typeof(IInventoryTreeClient);
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.ValidateDeviceConnectionAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("AddRouter.ProbeCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.ProbeResultText", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Probe\"", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/AddRouterWizardViewModel.cs");
        Assert.Contains("ValidateDeviceConnectionAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
    }

    /// <summary>W6-05: Inventory Probe refreshes tree so GetNode Reachability can surface.</summary>
    [Fact]
    public void Ac2eInventoryProbeRefreshesTreeAfterValidateDeviceConnection()
    {
        string wizard = ReadSource("src/Mfc.Desktop/ViewModels/AddRouterWizardViewModel.cs");
        Assert.Contains("ValidateDeviceConnectionAsync", wizard, StringComparison.Ordinal);
        Assert.Contains("RefreshCommand", wizard, StringComparison.Ordinal);
        Assert.Contains("finally", wizard, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", wizard, StringComparison.Ordinal);
    }

    /// <summary>W3.5: Zones panel edits definitions and resolves the selected Device.</summary>
    [Fact]
    public void Ac2dZonesEditDefinitionAndResolveDevice()
    {
        Type zones = typeof(ZonesViewModel);
        Assert.NotNull(zones.GetProperty("UpdateZoneCommand"));
        Assert.NotNull(zones.GetProperty("ResolveDeviceCommand"));
        Assert.NotNull(zones.GetProperty(nameof(ZonesViewModel.EditZoneName)));
        Assert.NotNull(zones.GetProperty(nameof(ZonesViewModel.EditZoneDescription)));

        Assert.NotNull(typeof(IZoneServiceClient).GetMethod(nameof(IZoneServiceClient.UpdateZoneDefinitionAsync)));
        Assert.NotNull(typeof(IZoneServiceClient).GetMethod(nameof(IZoneServiceClient.ResolveZonesForDeviceAsync)));
        Assert.NotNull(typeof(IZonePanelService).GetMethod(nameof(IZonePanelService.UpdateZoneAsync)));
        Assert.NotNull(typeof(IZonePanelService).GetMethod(nameof(IZonePanelService.ResolveForDeviceAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Zones.UpdateZoneCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.ResolveDeviceCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Update zone\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Resolve device\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Zones.EditZoneName", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/ZonesViewModel.cs");
        Assert.Contains("UpdateZoneAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("ResolveForDeviceAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
    }

    /// <summary>W4.3: Add router can create a VRRP Node and register two devices in one submit.</summary>
    [Fact]
    public void Ac2eAddRouterWizardCreatesVrrpNodeAndRegistersTwoDevices()
    {
        Type wizard = typeof(AddRouterWizardViewModel);
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.CreateAsVrrpPair)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.ShowVrrpPairFields)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.VrrpPairHint)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.PairMemberBDisplayName)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.PairMemberBManagementHost)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.PairMemberBManagementPortText)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("AddRouter.CreateAsVrrpPair", axaml, StringComparison.Ordinal);
        Assert.Contains("Create as VRRP pair", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.ShowVrrpPairFields", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.VrrpPairHint", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.PairMemberBDisplayName", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.PairMemberBManagementHost", axaml, StringComparison.Ordinal);
        Assert.Contains("Member b", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/AddRouterWizardViewModel.cs");
        Assert.Contains("NodeKind.Vrrp", vmSource, StringComparison.Ordinal);
        Assert.Contains("CreateAsVrrpPair", vmSource, StringComparison.Ordinal);
        Assert.Contains("RegisterAndConnectAsync", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Master/Backup", vmSource, StringComparison.Ordinal);
    }

    /// <summary>CONT-02: VRRP pair neighbor apply fills member a then empty member b; no auto-register.</summary>
    [Fact]
    public void Ac2fAddRouterNeighborApplyFillsVrrpMemberB()
    {
        Type wizard = typeof(AddRouterWizardViewModel);
        Assert.NotNull(wizard.GetProperty("ApplyNeighborCandidateCommand"));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.ShowVrrpPairFields)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.PairMemberBManagementHost)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.PairMemberBDisplayName)));
        Assert.NotNull(wizard.GetProperty(nameof(AddRouterWizardViewModel.PairMemberBManagementPortText)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("AddRouter.ApplyNeighborCandidateCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("AddRouter.PairMemberBManagementHost", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/AddRouterWizardViewModel.cs");
        Assert.Contains("ApplyNeighborToMemberB", vmSource, StringComparison.Ordinal);
        Assert.Contains("ShowVrrpPairFields", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Master/Backup", vmSource, StringComparison.Ordinal);
    }

    /// <summary>W5-01: Policies catalog ListPolicies → select → LoadRevision fills rules/objects.</summary>
    [Fact]
    public void Ac5dPoliciesCatalogBrowseListPoliciesThenSelectLoadsRevision()
    {
        Type policies = typeof(PoliciesViewModel);
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.Catalog)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.SelectedCatalogItem)));
        Assert.NotNull(policies.GetProperty("RefreshCatalogCommand"));
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.ListPoliciesAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.ListCatalogAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Policy catalog", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.RefreshCatalogCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.Catalog", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SelectedCatalogItem", axaml, StringComparison.Ordinal);
        Assert.Contains("Refresh catalog", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("ListCatalogAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("LoadRevisionAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("SelectedCatalogItem", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", vmSource, StringComparison.Ordinal);
    }

    /// <summary>W5-02: Policies bind ManagementPath/FastTrack hashes and findings from Controller RPC.</summary>
    [Fact]
    public void Ac5ePoliciesShowManagementPathAndFastTrackAnalysis()
    {
        Type policies = typeof(PoliciesViewModel);
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.ManagementPathFindingLines)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.FastTrackFindingLines)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.SafetyWitnessLines)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.ManagementPathContextHashText)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.FastTrackContextHashText)));
        Assert.NotNull(policies.GetProperty("RefreshSafetyAnalysisCommand"));
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.GetDevicePolicySafetyAnalysisAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.GetDevicePolicySafetyAnalysisAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Management path / FastTrack", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.RefreshSafetyAnalysisCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ManagementPathFindingLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.FastTrackFindingLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SafetyWitnessLines", axaml, StringComparison.Ordinal);
        Assert.Contains("controller source CIDR", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("GetDevicePolicySafetyAnalysisAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("ManagementPathContextHashText", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ManagementPathAnalysis", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FastTrackAnalysis", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Master/Backup", vmSource, StringComparison.Ordinal);
    }

    /// <summary>W6-01: empty policy catalog points operators at captured filter, not a missing analysis RPC.</summary>
    [Fact]
    public void Ac5fEmptyPolicyCatalogPointsAtCapturedFilter()
    {
        Assert.NotNull(typeof(PoliciesViewModel).GetProperty(nameof(PoliciesViewModel.HasEmptyCatalog)));
        Assert.NotNull(typeof(PoliciesViewModel).GetProperty(nameof(PoliciesViewModel.CapturedFilterHintText)));
        Assert.Contains("firewall.ipv4.filter", PoliciesViewModel.CapturedFilterHint, StringComparison.Ordinal);

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Policies.HasEmptyCatalog", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CapturedFilterHintText", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3NodeViewContainsTopologyZonesOnboardingAndReadiness()
    {
        Type node = typeof(NodeDetailViewModel);
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.TopologyText)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.ZoneSummaryLines)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.OnboardingReadinessText)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.DeploymentReadinessText)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.WorkflowStatusText)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.DeviceHashLines)));
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.Node)));
    }

    /// <summary>W1.6: Inventory/Node bind device fields already on the tree item (not DetailSummary-only).</summary>
    [Fact]
    public void Ac3bInventoryAndNodeShowExplicitDeviceFields()
    {
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.ReachabilityText)));
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.ModelText)));
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.RouterOsVersionText)));
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.VrrpRolesText)));
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.LastSnapshotText)));
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.HasVrrpRoles)));
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.ShowVrrpSurface)));
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.IsDevice)));
        Assert.NotNull(typeof(NodeDetailViewModel).GetProperty(nameof(NodeDetailViewModel.DeviceMembers)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Inventory.SelectedNode.ReachabilityText", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.SelectedNode.ModelText", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.SelectedNode.RouterOsVersionText", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.SelectedNode.LastSnapshotText", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.SelectedNode.ShowVrrpSurface", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.SelectedNode.VrrpRolesText", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.SelectedNode.DetailSummary", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.DeviceMembers", axaml, StringComparison.Ordinal);
        Assert.Contains("VRRP roles", axaml, StringComparison.Ordinal);
        Assert.Contains("Last snapshot", axaml, StringComparison.Ordinal);
        Assert.Contains("RouterOS version", axaml, StringComparison.Ordinal);
        Assert.Contains("Reachability", axaml, StringComparison.Ordinal);
    }

    /// <summary>W3.4: Node module calls GetNodeWorkflow instead of inventing readiness from Zones+Onboarding.</summary>
    [Fact]
    public void Ac3cNodeLoadsGetNodeWorkflowInsteadOfAdHocReadinessMashup()
    {
        Type node = typeof(NodeDetailViewModel);
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.WorkflowDeviceLines)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.HasWorkflowDeviceLines)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.HasNoWorkflowDeviceLines)));
        Assert.NotNull(typeof(IInventoryTreeClient).GetMethod(nameof(IInventoryTreeClient.GetNodeWorkflowAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Node.WorkflowDeviceLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Device workflow (GetNodeWorkflow)", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.HasNoWorkflowDeviceLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.HasWorkflowDeviceLines", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs");
        Assert.Contains("GetNodeWorkflowAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run", vmSource, StringComparison.Ordinal);
        Assert.Contains("ContributingStatus", vmSource, StringComparison.Ordinal);
        Assert.Contains("SyncClassification", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Zones hint=", vmSource, StringComparison.Ordinal);
    }

    /// <summary>W4.1: VRRP Node shows a/b members with role, management host, last capture — no invented roles.</summary>
    [Fact]
    public void Ac3dVrrpNodeShowsMemberTableRoleHostAndLastCapture()
    {
        Type node = typeof(NodeDetailViewModel);
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.IsVrrpNode)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.VrrpMembers)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.VrrpPairHint)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.SelectedVrrpMember)));
        Assert.NotNull(node.GetProperty(nameof(NodeDetailViewModel.HasVrrpMembers)));
        Assert.NotNull(typeof(VrrpMemberListItem).GetProperty(nameof(VrrpMemberListItem.SlotText)));
        Assert.NotNull(typeof(VrrpMemberListItem).GetProperty(nameof(VrrpMemberListItem.ManagementHostText)));
        Assert.NotNull(typeof(VrrpMemberListItem).GetProperty(nameof(VrrpMemberListItem.RoleText)));
        Assert.NotNull(typeof(VrrpMemberListItem).GetProperty(nameof(VrrpMemberListItem.LastSnapshotText)));
        Assert.NotNull(typeof(InventoryNodeViewModel).GetProperty(nameof(InventoryNodeViewModel.ManagementHostText)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("VRRP members", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.VrrpMembers", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.IsVrrpNode", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.VrrpPairHint", axaml, StringComparison.Ordinal);
        Assert.Contains("Node.SelectedVrrpMember", axaml, StringComparison.Ordinal);
        Assert.Contains("Management host", axaml, StringComparison.Ordinal);
        Assert.Contains("Last snapshot", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsVisible=\"{Binding HasRole}\"", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs");
        Assert.Contains("IsVrrpNode", vmSource, StringComparison.Ordinal);
        Assert.Contains("VrrpMemberListItem", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Master/Backup", vmSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4SnapshotViewShowsConfigurationAndObservations()
    {
        Type snapshot = typeof(SnapshotViewerViewModel);
        Assert.NotNull(snapshot.GetProperty(nameof(SnapshotViewerViewModel.ConfigurationRecords)));
        Assert.NotNull(snapshot.GetProperty(nameof(SnapshotViewerViewModel.ObservationRecords)));
        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Configuration records", axaml, StringComparison.Ordinal);
        Assert.Contains("Observation records", axaml, StringComparison.Ordinal);
        Assert.Contains("IsSnapshotsSelected", axaml, StringComparison.Ordinal);
    }

    /// <summary>W1.2: Snapshot viewer detail binds all Fields (not SummaryLine ≤4 only).</summary>
    [Fact]
    public void Ac4cSnapshotRecordDetailShowsAllFields()
    {
        Type snapshot = typeof(SnapshotViewerViewModel);
        Assert.NotNull(snapshot.GetProperty(nameof(SnapshotViewerViewModel.SelectedConfigurationRecord)));
        Assert.NotNull(snapshot.GetProperty(nameof(SnapshotViewerViewModel.SelectedObservationRecord)));
        Assert.NotNull(snapshot.GetProperty(nameof(SnapshotViewerViewModel.SelectedRecordDetail)));
        Assert.NotNull(snapshot.GetProperty(nameof(SnapshotViewerViewModel.SelectedRecordFields)));
        Assert.NotNull(snapshot.GetProperty(nameof(SnapshotViewerViewModel.HasSelectedRecord)));
        Assert.NotNull(typeof(SnapshotRecordListItem).GetProperty(nameof(SnapshotRecordListItem.Fields)));
        Assert.NotNull(typeof(SnapshotRecordListItem).GetProperty(nameof(SnapshotRecordListItem.HasMoreFields)));
        Assert.NotNull(typeof(SnapshotFieldLine).GetProperty(nameof(SnapshotFieldLine.DisplayLine)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Selected record fields", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.SelectedConfigurationRecord", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.SelectedObservationRecord", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.SelectedRecordFields", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.HasSelectedRecord", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.HasNoSelectedRecord", axaml, StringComparison.Ordinal);
        Assert.Contains("DisplayLine", axaml, StringComparison.Ordinal);
        Assert.Contains("Select a record to see all fields.", axaml, StringComparison.Ordinal);
    }

    /// <summary>W3.1: Snapshots Capture starts device capture and binds WatchCapture progress.</summary>
    [Fact]
    public void Ac4dSnapshotCaptureStartsAndWatchesProgress()
    {
        Type snapshot = typeof(SnapshotViewerViewModel);
        Assert.NotNull(snapshot.GetProperty("CaptureCommand"));
        Assert.NotNull(snapshot.GetProperty(nameof(SnapshotViewerViewModel.CaptureProgressText)));
        Assert.NotNull(snapshot.GetProperty(nameof(SnapshotViewerViewModel.IsCapturing)));

        Type client = typeof(ISnapshotViewerClient);
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.StartCaptureAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.StartNodeCaptureAsync)));
        Assert.NotNull(client.GetMethod(nameof(ISnapshotViewerClient.WatchCaptureAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Snapshot.CaptureCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.CaptureProgressText", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Capture\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Capture progress:", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs");
        Assert.Contains("StartCaptureAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("WatchCaptureAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("node_id", vmSource, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>W4.4: VRRP Snapshots capture per member; Compare shows why a-against-b is forbidden.</summary>
    [Fact]
    public void Ac4eVrrpPairCaptureIsPerMemberAndCompareShowsCrossDeviceForbidWhy()
    {
        Assert.NotNull(typeof(SnapshotViewerViewModel).GetProperty(nameof(SnapshotViewerViewModel.PairGuidanceText)));
        Assert.NotNull(typeof(SnapshotViewerViewModel).GetProperty(nameof(SnapshotViewerViewModel.HasVrrpPairGuidance)));
        Assert.NotNull(typeof(SnapshotDiffViewModel).GetProperty(nameof(SnapshotDiffViewModel.PairGuidanceText)));
        Assert.NotNull(typeof(SnapshotDiffViewModel).GetProperty(nameof(SnapshotDiffViewModel.HasVrrpPairGuidance)));
        Assert.Equal(
            "VRRP capture is per member. Select Device a or b in the tree; Capture does not run against the Node (no silent first child).",
            InventoryOpsSelection.VrrpPairCaptureNodeHint);
        Assert.Contains(
            "SNAPSHOTS_FROM_DIFFERENT_DEVICES",
            InventoryOpsSelection.CrossDeviceCompareForbiddenReason,
            StringComparison.Ordinal);

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Snapshot.PairGuidanceText", axaml, StringComparison.Ordinal);
        Assert.Contains("Snapshot.HasVrrpPairGuidance", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.PairGuidanceText", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.HasVrrpPairGuidance", axaml, StringComparison.Ordinal);

        string snapshot = ReadSource("src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs");
        string diff = ReadSource("src/Mfc.Desktop/ViewModels/SnapshotDiffViewModel.cs");
        string selection = ReadSource("src/Mfc.Desktop/ViewModels/InventoryOpsSelection.cs");
        Assert.Contains("FormatCaptureGuidance", snapshot, StringComparison.Ordinal);
        Assert.Contains("FormatCompareGuidance", diff, StringComparison.Ordinal);
        Assert.Contains("ExplainCompareError", diff, StringComparison.Ordinal);
        Assert.Contains("StartCaptureAsync", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", diff, StringComparison.Ordinal);
        Assert.DoesNotContain("node_id", snapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Does not run SemanticDiffEngine locally", diff, StringComparison.Ordinal);
        Assert.DoesNotContain("Master/Backup", selection, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOTS_FROM_DIFFERENT_DEVICES", selection, StringComparison.Ordinal);
    }

    /// <summary>W1.1: Semantic diff binds FieldLines + Compare warnings (not RecordKey-only).</summary>
    [Fact]
    public void Ac4bSemanticDiffShowsFieldLinesAndWarnings()
    {
        Type diff = typeof(SnapshotDiffViewModel);
        Assert.NotNull(diff.GetProperty(nameof(SnapshotDiffViewModel.Warnings)));
        Assert.NotNull(diff.GetProperty(nameof(SnapshotDiffViewModel.HasWarnings)));
        Assert.NotNull(typeof(SnapshotDiffEntryItem).GetProperty(nameof(SnapshotDiffEntryItem.FieldLines)));
        Assert.NotNull(typeof(SnapshotDiffFieldLine).GetProperty(nameof(SnapshotDiffFieldLine.Summary)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Semantic diff", axaml, StringComparison.Ordinal);
        Assert.Contains("FieldLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Compare warnings", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.HasWarnings", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.VisibleWarnings", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.HasWarningOverflow", axaml, StringComparison.Ordinal);
        Assert.Contains("DisplayIdentity", ReadSource("src/Mfc.Desktop/Services/SnapshotDiffModels.cs"), StringComparison.Ordinal);
        Assert.Contains("IsFingerprintKey", ReadSource("src/Mfc.Desktop/Services/SnapshotPresentationIdentity.cs"), StringComparison.Ordinal);
    }

    /// <summary>W6-01: Diff/Snapshot identity is field/ordinal, not fingerprint hex; firewall.filter is first-class.</summary>
    [Fact]
    public void Ac4gOperatorReadableDiffAndFirewallSectionDefault()
    {
        Assert.NotNull(typeof(SnapshotDiffEntryItem).GetProperty(nameof(SnapshotDiffEntryItem.DisplayIdentity)));
        Assert.True(SnapshotPresentationIdentity.IsFingerprintKey(new string('a', 64)));
        Assert.Equal(
            "firewall.ipv4.filter",
            SnapshotPresentationIdentity.OperatorFacingSectionIds[0]);

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("FieldLines", axaml, StringComparison.Ordinal);
        Assert.Contains("firewall.ipv4.filter", ReadSource("src/Mfc.Desktop/Services/SnapshotPresentationIdentity.cs"), StringComparison.Ordinal);
        Assert.Contains("PreferOperatorFacingSection", ReadSource("src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"), StringComparison.Ordinal);
        Assert.Contains("CompareAsync().ConfigureAwait(true)", ReadSource("src/Mfc.Desktop/ViewModels/SnapshotDiffViewModel.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", ReadSource("src/Mfc.Desktop/Services/SnapshotPresentationIdentity.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", ReadSource("src/Mfc.Desktop/Services/SnapshotPresentationIdentity.cs"), StringComparison.Ordinal);
    }

    /// <summary>W2.1: Diff binds Before/After SnapshotRecord detail; Compare warnings truncate.</summary>
    [Fact]
    public void Ac4fSemanticDiffShowsBeforeAfterRecordsAndTruncatesWarnings()
    {
        Assert.NotNull(typeof(SnapshotDiffEntryItem).GetProperty(nameof(SnapshotDiffEntryItem.BeforeRecordFields)));
        Assert.NotNull(typeof(SnapshotDiffEntryItem).GetProperty(nameof(SnapshotDiffEntryItem.AfterRecordFields)));
        Assert.NotNull(typeof(SnapshotDiffEntryItem).GetProperty(nameof(SnapshotDiffEntryItem.HasRecordSides)));
        Assert.NotNull(typeof(SnapshotDiffViewModel).GetProperty(nameof(SnapshotDiffViewModel.SelectedEntry)));
        Assert.NotNull(typeof(SnapshotDiffViewModel).GetProperty(nameof(SnapshotDiffViewModel.VisibleWarnings)));
        Assert.NotNull(typeof(SnapshotDiffViewModel).GetProperty(nameof(SnapshotDiffViewModel.HasWarningOverflow)));
        Assert.Equal(12, SnapshotDiffService.MaxVisibleCompareWarnings);

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Before record", axaml, StringComparison.Ordinal);
        Assert.Contains("After record", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.SelectedEntry", axaml, StringComparison.Ordinal);
        Assert.Contains("BeforeRecordFields", axaml, StringComparison.Ordinal);
        Assert.Contains("AfterRecordFields", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.VisibleWarnings", axaml, StringComparison.Ordinal);
        Assert.Contains("Diff.WarningOverflowText", axaml, StringComparison.Ordinal);

        string service = ReadSource("src/Mfc.Desktop/Services/SnapshotDiffService.cs");
        string diff = ReadSource("src/Mfc.Desktop/ViewModels/SnapshotDiffViewModel.cs");
        string client = ReadSource("src/Mfc.Desktop/Services/GrpcSnapshotViewerClient.cs");
        Assert.Contains("MapRecordFields", service, StringComparison.Ordinal);
        Assert.Contains("IsCredentialFieldName", service, StringComparison.Ordinal);
        Assert.Contains("TakeVisibleWarnings", service, StringComparison.Ordinal);
        Assert.Contains("seenWarnings", client, StringComparison.Ordinal);
        Assert.Contains("Does not run SemanticDiffEngine locally", diff, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", service, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", diff, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5PolicyViewSupportsAuthoringReviewAndBinding()
    {
        Type policies = typeof(PoliciesViewModel);
        Assert.NotNull(policies.GetProperty("CreateDraftCommand"));
        Assert.NotNull(policies.GetProperty("ValidateCommand"));
        Assert.NotNull(policies.GetProperty("SubmitCommand"));
        Assert.NotNull(policies.GetProperty("ApproveCommand"));
        Assert.NotNull(policies.GetProperty("BindCommand"));
        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Policy authoring / review / binding", axaml, StringComparison.Ordinal);
    }

    /// <summary>W1.3: Policies binds catalog lists + DiffLines; Compose defaults from inventory Node.</summary>
    [Fact]
    public void Ac5bPoliciesBindCatalogListsAndComposeFromSelectedNode()
    {
        Type policies = typeof(PoliciesViewModel);
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.AddressObjects)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.ServiceObjects)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.ChainContracts)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.DiffLines)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.ComposeNodeIdText)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.RevisionIdText)));
        Assert.NotNull(policies.GetProperty("CreateDraftCommand"));
        Assert.NotNull(policies.GetProperty("UpsertAddressCommand"));
        Assert.NotNull(policies.GetProperty("UpsertServiceCommand"));
        Assert.NotNull(policies.GetProperty("ReplaceContractsCommand"));
        Assert.NotNull(policies.GetProperty("RecordAnalysisCommand"));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Address objects", axaml, StringComparison.Ordinal);
        Assert.Contains("Service objects", axaml, StringComparison.Ordinal);
        Assert.Contains("Chain contracts", axaml, StringComparison.Ordinal);
        Assert.Contains("Revision diff", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.AddressObjects", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ServiceObjects", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ChainContracts", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.UpsertAddressCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.ComposeNodeIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CreateDraftCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.LoadCommand", axaml, StringComparison.Ordinal);
    }

    /// <summary>W6-06: Policies Revision diff binds typed kind/detail rows; DiffLines stay secondary.</summary>
    [Fact]
    public void Ac5gPoliciesRevisionDiffBindsTypedKindDetailRows()
    {
        Assert.NotNull(typeof(PoliciesViewModel).GetProperty(nameof(PoliciesViewModel.DiffRows)));
        Assert.NotNull(typeof(PoliciesViewModel).GetProperty(nameof(PoliciesViewModel.DiffLines)));
        Assert.NotNull(typeof(PolicyDiffRowListItem).GetProperty(nameof(PolicyDiffRowListItem.KindText)));
        Assert.NotNull(typeof(PolicyDiffRowListItem).GetProperty(nameof(PolicyDiffRowListItem.DetailText)));
        Assert.NotNull(typeof(PolicyDiffRowListItem).GetProperty(nameof(PolicyDiffRowListItem.SummaryLine)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Policies.DiffRows", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffLines", axaml, StringComparison.Ordinal);
        Assert.Contains("KindText", axaml, StringComparison.Ordinal);
        Assert.Contains("DetailText", axaml, StringComparison.Ordinal);

        string service = ReadSource("src/Mfc.Desktop/Services/PolicyPanelService.cs");
        Assert.Contains("PolicyDiffRowListItem", service, StringComparison.Ordinal);
        Assert.Contains("KindText = \"semantic\"", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", service, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", service, StringComparison.Ordinal);
    }

    /// <summary>W6-07: Diff baseline revision can be chosen from catalog without UUID paste / LoadRevision.</summary>
    [Fact]
    public void Ac5hPoliciesDiffBaselinePicksFromCatalogWithoutUuidRitual()
    {
        Assert.NotNull(typeof(PoliciesViewModel).GetProperty(nameof(PoliciesViewModel.DiffBaselineCatalogItem)));
        Assert.NotNull(typeof(PoliciesViewModel).GetProperty(nameof(PoliciesViewModel.DiffBaselineRevisionIdText)));
        Assert.NotNull(typeof(PoliciesViewModel).GetProperty(nameof(PoliciesViewModel.Catalog)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Policies.DiffBaselineCatalogItem", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DiffBaselineRevisionIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.Catalog", axaml, StringComparison.Ordinal);

        string vm = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("OnDiffBaselineCatalogItemChanged", vm, StringComparison.Ordinal);
        Assert.Contains("DiffBaselineRevisionIdText = value.LatestRevisionId", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", vm, StringComparison.Ordinal);
    }

    /// <summary>W6-09: stage reorder via Move up/down on selected rule (no UUID paste ritual).</summary>
    [Fact]
    public void Ac5iPoliciesReorderMovesSelectedRuleWithoutUuidPaste()
    {
        Type policies = typeof(PoliciesViewModel);
        Assert.NotNull(policies.GetProperty("MoveRuleUpCommand"));
        Assert.NotNull(policies.GetProperty("MoveRuleDownCommand"));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.SelectedRule)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.ReorderRuleIdsText)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Policies.MoveRuleUpCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.MoveRuleDownCommand", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Policies.ReorderRuleIdsText", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("MoveSelectedRuleAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("ReorderRulesInStageAsync", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", vmSource, StringComparison.Ordinal);
    }

    /// <summary>W3.6: Policies mutate rules, ack recorded warnings, and compile semantic artifacts.</summary>
    [Fact]
    public void Ac5cPoliciesMutateRulesAckWarningsAndCompile()
    {
        Type policies = typeof(PoliciesViewModel);
        Assert.NotNull(policies.GetProperty("UpdateRuleCommand"));
        Assert.NotNull(policies.GetProperty("DeleteRuleCommand"));
        Assert.NotNull(policies.GetProperty("AcknowledgeWarningCommand"));
        Assert.NotNull(policies.GetProperty("CompileCommand"));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.SelectedRule)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.SelectedFinding)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.CompileCapabilityHashText)));
        Assert.NotNull(policies.GetProperty(nameof(PoliciesViewModel.CompileArtifactLines)));

        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.UpdateRuleAsync)));
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.DeleteRuleAsync)));
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.AcknowledgeWarningAsync)));
        Assert.NotNull(typeof(IPolicyServiceClient).GetMethod(nameof(IPolicyServiceClient.CompileNodeFilterArtifactsAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.UpdateRuleAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.DeleteRuleAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.AcknowledgeWarningAsync)));
        Assert.NotNull(typeof(IPolicyPanelService).GetMethod(nameof(IPolicyPanelService.CompileNodeFilterArtifactsAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Policies.UpdateRuleCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.DeleteRuleCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.AcknowledgeWarningCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CompileCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SelectedRule", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.SelectedFinding", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CompileCapabilityHashText", axaml, StringComparison.Ordinal);
        Assert.Contains("Policies.CompileArtifactLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Update rule\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Delete rule\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Acknowledge warning\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Compile artifacts\"", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs");
        Assert.Contains("UpdateRuleAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("DeleteRuleAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("AcknowledgeWarningAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("CompileNodeFilterArtifactsAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAndDeploy", vmSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6OperationsViewSupportsOnboardingDeploymentAndRecovery()
    {
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.Onboarding)));
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.Deployment)));
        Assert.NotNull(typeof(OnboardingViewModel).GetProperty("RecoveryCommand"));
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty("RecoveryCommand"));
        string axaml = ReadMainWindowAxaml();
        Assert.Contains("IsOperationsSelected", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding", axaml, StringComparison.Ordinal);
        Assert.Contains("Deploy", axaml, StringComparison.Ordinal);
        Assert.Contains("Recovery status", axaml, StringComparison.Ordinal);
    }

    /// <summary>W1.4: Operations binds plan collections already held in VM (not SemanticDiffLines-only).</summary>
    [Fact]
    public void Ac6bOperationsShowsPlanCollectionsNotOnlyHashDelta()
    {
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.ArtifactLines)));
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.OrderLines)));
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.ProbeAndWatchdogLines)));
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.SemanticDiffLines)));
        Assert.NotNull(typeof(OnboardingViewModel).GetProperty(nameof(OnboardingViewModel.Placements)));
        Assert.NotNull(typeof(OnboardingPlacementListItem).GetProperty(nameof(OnboardingPlacementListItem.SummaryLine)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Artifact hash delta", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.SemanticDiffLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.ArtifactLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.OrderLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.ProbeAndWatchdogLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Anchor placements", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.Placements", axaml, StringComparison.Ordinal);
        Assert.Contains("Activation / rollback order", axaml, StringComparison.Ordinal);
        Assert.Contains("Probes and watchdog", axaml, StringComparison.Ordinal);
    }

    /// <summary>W3.3: Operations Start consumes Watch streams, not only Start.Timeline snapshot.</summary>
    [Fact]
    public void Ac6cOperationsStartWatchesOnboardingAndDeploymentProgress()
    {
        Assert.NotNull(typeof(OnboardingViewModel).GetProperty(nameof(OnboardingViewModel.ProgressLines)));
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.ProgressLines)));
        Assert.NotNull(typeof(IOnboardingServiceClient).GetMethod(nameof(IOnboardingServiceClient.WatchAsync)));
        Assert.NotNull(typeof(IDeploymentServiceClient).GetMethod(nameof(IDeploymentServiceClient.WatchAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Onboarding.ProgressLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.ProgressLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.StartCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.StartCommand", axaml, StringComparison.Ordinal);

        string onboarding = ReadSource("src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs");
        string deployment = ReadSource("src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs");
        Assert.Contains("WatchAsync", onboarding, StringComparison.Ordinal);
        Assert.Contains("WatchAsync", deployment, StringComparison.Ordinal);
        Assert.Contains("Task.Run", onboarding, StringComparison.Ordinal);
        Assert.Contains("Task.Run", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", onboarding, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", deployment, StringComparison.Ordinal);
    }

    /// <summary>CONT-01: Deployment Rollback consumes Watch, not only Rollback.Timeline snapshot.</summary>
    [Fact]
    public void Ac6eDeploymentRollbackWatchesProgress()
    {
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.ProgressLines)));
        Assert.NotNull(typeof(IDeploymentServiceClient).GetMethod(nameof(IDeploymentServiceClient.WatchAsync)));
        Assert.NotNull(typeof(IDeploymentServiceClient).GetMethod(nameof(IDeploymentServiceClient.RollbackAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Deployment.RollbackCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.ProgressLines", axaml, StringComparison.Ordinal);

        string deployment = ReadSource("src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs");
        Assert.Contains("RollbackAndWatchAsync", deployment, StringComparison.Ordinal);
        Assert.Contains("WatchAsync", deployment, StringComparison.Ordinal);
        Assert.Contains("Task.Run", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SemanticDiffEngine",
            deployment.Replace("Does not run SemanticDiffEngine locally", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    /// <summary>W6-04: Onboarding Rollback consumes Watch, not only Rollback.Timeline snapshot.</summary>
    [Fact]
    public void Ac6gOnboardingRollbackWatchesProgress()
    {
        Assert.NotNull(typeof(OnboardingViewModel).GetProperty(nameof(OnboardingViewModel.ProgressLines)));
        Assert.NotNull(typeof(IOnboardingServiceClient).GetMethod(nameof(IOnboardingServiceClient.WatchAsync)));
        Assert.NotNull(typeof(IOnboardingServiceClient).GetMethod(nameof(IOnboardingServiceClient.RollbackAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Onboarding.RollbackCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.ProgressLines", axaml, StringComparison.Ordinal);

        string onboarding = ReadSource("src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs");
        Assert.Contains("RollbackAndWatchAsync", onboarding, StringComparison.Ordinal);
        Assert.Contains("WatchAsync", onboarding, StringComparison.Ordinal);
        Assert.Contains("Task.Run", onboarding, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", onboarding, StringComparison.Ordinal);
    }

    /// <summary>W5-03: Operations binds typed semantic diff kind/path/before/after; hash delta stays secondary.</summary>
    [Fact]
    public void Ac6fDeploymentPlanBindsTypedSemanticDiffRows()
    {
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.SemanticDiffRows)));
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.SemanticDiffLines)));
        Assert.NotNull(typeof(DeploymentSemanticDiffListItem).GetProperty(nameof(DeploymentSemanticDiffListItem.KindText)));
        Assert.NotNull(typeof(DeploymentSemanticDiffListItem).GetProperty(nameof(DeploymentSemanticDiffListItem.PathText)));
        Assert.NotNull(typeof(DeploymentSemanticDiffListItem).GetProperty(nameof(DeploymentSemanticDiffListItem.BeforeText)));
        Assert.NotNull(typeof(DeploymentSemanticDiffListItem).GetProperty(nameof(DeploymentSemanticDiffListItem.AfterText)));
        Assert.NotNull(typeof(DeploymentSemanticDiffListItem).GetProperty(nameof(DeploymentSemanticDiffListItem.HashDeltaText)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Deployment.SemanticDiffRows", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.SemanticDiffLines", axaml, StringComparison.Ordinal);
        Assert.Contains("KindText", axaml, StringComparison.Ordinal);
        Assert.Contains("PathText", axaml, StringComparison.Ordinal);
        Assert.Contains("BeforeText", axaml, StringComparison.Ordinal);
        Assert.Contains("AfterText", axaml, StringComparison.Ordinal);
        Assert.Contains("Artifact hash delta", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("until a richer policy diff exists", axaml, StringComparison.Ordinal);

        string deployment = ReadSource("src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs");
        Assert.Contains("Does not run SemanticDiffEngine locally", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticDiffEngine", deployment.Replace("Does not run SemanticDiffEngine locally", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    /// <summary>W4.2: VRRP Operations target the Node pair (all members), not a silent first Device child.</summary>
    [Fact]
    public void Ac6dOperationsTargetVrrpNodePairNotSilentFirstDevice()
    {
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.TargetHint)));
        Assert.NotNull(typeof(DeploymentViewModel).GetProperty(nameof(DeploymentViewModel.HasVrrpPairTarget)));
        Assert.NotNull(typeof(OnboardingViewModel).GetProperty(nameof(OnboardingViewModel.TargetHint)));
        Assert.NotNull(typeof(OnboardingViewModel).GetProperty(nameof(OnboardingViewModel.HasVrrpPairTarget)));
        Assert.Equal(
            "VRRP ops target this Node (pair). Create plan includes all members; the first Device is not used silently.",
            InventoryOpsSelection.VrrpPairHint);

        MethodInfo? deployRequireDevice = typeof(DeploymentViewModel).GetMethod(
            "RequireDeviceId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? onboardRequireDevice = typeof(OnboardingViewModel).GetMethod(
            "RequireDeviceId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Null(deployRequireDevice);
        Assert.Null(onboardRequireDevice);

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Deployment.TargetHint", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.TargetHint", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.HasVrrpPairTarget", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.HasVrrpPairTarget", axaml, StringComparison.Ordinal);

        string onboarding = ReadSource("src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs");
        string deployment = ReadSource("src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs");
        string selection = ReadSource("src/Mfc.Desktop/ViewModels/InventoryOpsSelection.cs");
        Assert.Contains("InventoryOpsSelection.RequireDeviceIds", onboarding, StringComparison.Ordinal);
        Assert.Contains("InventoryOpsSelection.RequireDeviceIds", deployment, StringComparison.Ordinal);
        Assert.Contains("RequireDeviceIds", selection, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstOrDefault", onboarding, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstOrDefault", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", onboarding, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", selection, StringComparison.Ordinal);
        Assert.DoesNotContain("Master/Backup", selection, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac7DriftViewHasNoAutomaticFix()
    {
        Type drift = typeof(DriftViewModel);
        Assert.False((bool)drift.GetProperty(nameof(DriftViewModel.HasAutomaticFix))!
            .GetValue(CreateDriftVmForFlags())!);
        Assert.False((bool)drift.GetProperty(nameof(DriftViewModel.HasForceRepairCommand))!
            .GetValue(CreateDriftVmForFlags())!);
        Assert.False((bool)drift.GetProperty(nameof(DriftViewModel.HasAutoHealCommand))!
            .GetValue(CreateDriftVmForFlags())!);

        Assert.Null(drift.GetMethod("ForceRepair"));
        Assert.Null(drift.GetMethod("AutoHeal"));
        Assert.Null(drift.GetMethod("FixAll"));
        Assert.Null(drift.GetProperty("ForceRepairCommand"));
        Assert.Null(drift.GetProperty("AutoHealCommand"));
        Assert.Null(drift.GetProperty("FixAllAutomaticallyCommand"));

        string axaml = ReadMainWindowAxaml();
        Assert.DoesNotContain("ForceRepair", axaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AutoHeal", axaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fix all automatically", axaml, StringComparison.OrdinalIgnoreCase);

        foreach (string name in DriftService.Descriptor.Methods.Select(static m => m.Name))
        {
            Assert.DoesNotContain("Repair", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Heal", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Fix", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>W1.5: Drift list maps findings (kind/severity/detail); SemanticDiffText is not the only content.</summary>
    [Fact]
    public void Ac7bDriftShowsFindingsFromListResponseNotOnlySemanticDiff()
    {
        Assert.NotNull(typeof(DriftEventListItem).GetProperty(nameof(DriftEventListItem.Findings)));
        Assert.NotNull(typeof(DriftFindingListItem).GetProperty(nameof(DriftFindingListItem.KindText)));
        Assert.NotNull(typeof(DriftFindingListItem).GetProperty(nameof(DriftFindingListItem.SeverityText)));
        Assert.NotNull(typeof(DriftFindingListItem).GetProperty(nameof(DriftFindingListItem.Detail)));
        Assert.NotNull(typeof(DriftViewModel).GetProperty(nameof(DriftViewModel.SelectedEventFindings)));
        Assert.NotNull(typeof(DriftViewModel).GetProperty(nameof(DriftViewModel.SemanticDiffText)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Drift findings", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.SelectedEventFindings", axaml, StringComparison.Ordinal);
        Assert.Contains("KindText", axaml, StringComparison.Ordinal);
        Assert.Contains("SeverityText", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.SemanticDiffText", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/DriftViewModel.cs");
        Assert.Contains("evt.Findings.Select(DriftFindingListItem.FromProto)", vmSource, StringComparison.Ordinal);
    }

    /// <summary>W3.7: selecting a drift event loads GetDriftEvent for the full payload (not truncated list hashes).</summary>
    [Fact]
    public void Ac7cDriftLoadsGetDriftEventForSelectedPayload()
    {
        Type drift = typeof(DriftViewModel);
        Assert.NotNull(drift.GetProperty(nameof(DriftViewModel.DetailNodeIdText)));
        Assert.NotNull(drift.GetProperty(nameof(DriftViewModel.DetailBaselineHashText)));
        Assert.NotNull(drift.GetProperty(nameof(DriftViewModel.DetailActualHashText)));
        Assert.NotNull(drift.GetProperty(nameof(DriftViewModel.DetailDesiredHashText)));
        Assert.NotNull(drift.GetProperty(nameof(DriftViewModel.DetailSemanticDiffHashText)));
        Assert.NotNull(drift.GetProperty(nameof(DriftViewModel.DetailImmutableText)));
        Assert.NotNull(drift.GetProperty(nameof(DriftViewModel.HasSelectedEventDetail)));
        Assert.NotNull(drift.GetProperty(nameof(DriftViewModel.HasNoSelectedEventDetail)));
        Assert.NotNull(typeof(IDriftServiceClient).GetMethod(nameof(IDriftServiceClient.GetDriftEventAsync)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Event detail (GetDriftEvent)", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.DetailNodeIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.DetailBaselineHashText", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.DetailActualHashText", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.DetailDesiredHashText", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.DetailSemanticDiffHashText", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.DetailImmutableText", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.HasSelectedEventDetail", axaml, StringComparison.Ordinal);
        Assert.Contains("Drift.HasNoSelectedEventDetail", axaml, StringComparison.Ordinal);

        string vmSource = ReadSource("src/Mfc.Desktop/ViewModels/DriftViewModel.cs");
        Assert.Contains("GetDriftEventAsync", vmSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run", vmSource, StringComparison.Ordinal);
        Assert.Contains("_detailEpoch", vmSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vmSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac8AuditIsReadOnly()
    {
        Type audit = typeof(AuditViewModel);
        Assert.True((bool)audit.GetProperty(nameof(AuditViewModel.IsReadOnly))!
            .GetValue(CreateAuditVmForFlags())!);
        Assert.False((bool)audit.GetProperty(nameof(AuditViewModel.HasWriteCommands))!
            .GetValue(CreateAuditVmForFlags())!);
        Assert.Null(audit.GetMethod("Append"));
        Assert.Null(audit.GetMethod("Write"));
        Assert.Null(audit.GetMethod("Delete"));
        Assert.Null(audit.GetProperty("AppendCommand"));
        Assert.Null(audit.GetProperty("DeleteCommand"));

        string[] methods = AuditService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["ListAuditEvents"], methods);
        Assert.DoesNotContain(methods, m => m.Contains("Write", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Append", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ac9UiThreadNeverPerformsRemoteIo()
    {
        string driftSource = ReadSource("src/Mfc.Desktop/ViewModels/DriftViewModel.cs");
        string auditSource = ReadSource("src/Mfc.Desktop/ViewModels/AuditViewModel.cs");
        string shellSource = ReadSource("src/Mfc.Desktop/ViewModels/ShellViewModel.cs");
        Assert.Contains("Task.Run", driftSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run", auditSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run", shellSource, StringComparison.Ordinal);
        Assert.Contains("ConfigureAwait", driftSource, StringComparison.Ordinal);
        Assert.Contains("ConfigureAwait", auditSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac10CachedStateIsClearlyMarked()
    {
        Assert.NotNull(typeof(InventoryTreeViewModel).GetProperty(nameof(InventoryTreeViewModel.IsCached)));
        Assert.NotNull(typeof(InventoryTreeViewModel).GetProperty(nameof(InventoryTreeViewModel.CachedBadgeText)));
        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Inventory.IsCached", axaml, StringComparison.Ordinal);
        Assert.Contains("Inventory.CachedBadgeText", axaml, StringComparison.Ordinal);
        Assert.NotNull(typeof(DriftViewModel).GetProperty(nameof(DriftViewModel.CachedBadgeText)));
        Assert.NotNull(typeof(AuditViewModel).GetProperty(nameof(AuditViewModel.CachedBadgeText)));
    }

    [Fact]
    public void Ac11DesktopHasNoRouterOsOrSqlDependencies()
    {
        Assembly desktop = typeof(MainWindow).Assembly;
        string[] forbidden =
        [
            "Mfc.Domain",
            "Mfc.Application",
            "Mfc.Infrastructure",
            "Mfc.RouterOs",
            "Npgsql",
            "Microsoft.EntityFrameworkCore",
        ];
        foreach (AssemblyName reference in desktop.GetReferencedAssemblies())
        {
            Assert.DoesNotContain(forbidden, f => string.Equals(reference.Name, f, StringComparison.Ordinal));
        }

        Assert.Contains(
            desktop.GetReferencedAssemblies(),
            r => string.Equals(r.Name, "Mfc.Contracts", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac12KeyboardNavigationAndLargeListVirtualization()
    {
        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Window.KeyBindings", axaml, StringComparison.Ordinal);
        Assert.Contains("Ctrl+1", axaml, StringComparison.Ordinal);
        Assert.Contains("Ctrl+7", axaml, StringComparison.Ordinal);
        Assert.Contains("SelectModuleCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("HotKeysText", axaml, StringComparison.Ordinal);
        Assert.Contains(nameof(ShellViewModel.HotKeysText), typeof(ShellViewModel).GetProperties().Select(p => p.Name));

        Assert.Matches(@"Name=""DriftEventsList""[\s\S]*VirtualizingStackPanel", axaml);
        Assert.Matches(@"Name=""AuditEventsList""[\s\S]*VirtualizingStackPanel", axaml);

        Assert.True(typeof(InputElement).IsAssignableFrom(typeof(Window)));
        Assert.True(typeof(MainWindow).IsSubclassOf(typeof(Window)));
    }

    private static DriftViewModel CreateDriftVmForFlags()
    {
        return new DriftViewModel(
            new NullDriftClient(),
            new NullConnection(),
            new InventoryTreeViewModel(new NullInventoryTree(), new NullConnection()));
    }

    private static AuditViewModel CreateAuditVmForFlags()
        => new(new NullAuditClient(), new NullConnection());

    private static string ReadMainWindowAxaml()
        => ReadSource("src/Mfc.Desktop/MainWindow.axaml");

    private static string ReadSource(string relativePath)
    {
        string root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, relativePath));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class NullConnection : IControllerConnectionService
    {
        public Grpc.Net.Client.GrpcChannel? Channel => null;

        public ControllerConnectionState State => ControllerConnectionState.Disconnected;

        public string? LastError => null;

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NullInventoryTree : IInventoryTreeService
    {
        public InventoryTreeLoadResult Current { get; } = new()
        {
            Roots = [],
            Succeeded = true,
            IsCached = false,
            IsRefreshing = false,
        };

        public Task<InventoryTreeLoadResult> RefreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Current);
    }

    private sealed class NullDriftClient : IDriftServiceClient
    {
        public Task<IReadOnlyList<DriftEvent>> ListDeviceDriftEventsAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DriftEvent>>([]);

        public Task<DriftEvent> GetDriftEventAsync(
            Guid driftEventId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DriftEvent());
    }

    private sealed class NullAuditClient : IAuditServiceClient
    {
        public Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
            uint pageSize = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditEvent>>([]);
    }
}
