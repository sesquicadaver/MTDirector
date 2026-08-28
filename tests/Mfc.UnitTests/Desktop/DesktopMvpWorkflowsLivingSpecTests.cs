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

        Type inventory = typeof(IInventoryTreeClient);
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.CreateSiteAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.CreateNodeAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.RegisterDeviceAsync)));
        Assert.NotNull(inventory.GetMethod(nameof(IInventoryTreeClient.UpdateDeviceConnectionAsync)));
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
