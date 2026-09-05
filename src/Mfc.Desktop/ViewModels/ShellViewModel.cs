using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>Shell view-model: connection + seven MVP modules (M6-04).</summary>
public sealed partial class ShellViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public ShellViewModel(
        IControllerConnectionService connection,
        DesktopOptions options,
        InventoryTreeViewModel inventory,
        AddRouterWizardViewModel addRouter,
        NodeDetailViewModel node,
        SnapshotViewerViewModel snapshot,
        SnapshotDiffViewModel diff,
        ZonesViewModel zones,
        PoliciesViewModel policies,
        OnboardingViewModel onboarding,
        DeploymentViewModel deployment,
        DriftViewModel drift,
        AuditViewModel audit,
        RoutingAssuranceViewModel routingAssurance)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        AddRouter = addRouter ?? throw new ArgumentNullException(nameof(addRouter));
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Diff = diff ?? throw new ArgumentNullException(nameof(diff));
        Zones = zones ?? throw new ArgumentNullException(nameof(zones));
        Policies = policies ?? throw new ArgumentNullException(nameof(policies));
        Onboarding = onboarding ?? throw new ArgumentNullException(nameof(onboarding));
        Deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        Drift = drift ?? throw new ArgumentNullException(nameof(drift));
        Audit = audit ?? throw new ArgumentNullException(nameof(audit));
        RoutingAssurance = routingAssurance ?? throw new ArgumentNullException(nameof(routingAssurance));
        Modules =
        [
            ShellNavigationModule.Inventory,
            ShellNavigationModule.Node,
            ShellNavigationModule.Snapshots,
            ShellNavigationModule.Policies,
            ShellNavigationModule.Operations,
            ShellNavigationModule.Drift,
            ShellNavigationModule.Audit,
        ];
        SelectedModule = ShellNavigationModule.Inventory;
        _connection.StateChanged += OnConnectionStateChanged;
        SyncFromService();
    }

    /// <summary>Exact seven MVP module names in navigation order.</summary>
    public IReadOnlyList<ShellNavigationModule> Modules { get; }

    /// <summary>Documented keyboard shortcuts for Living Spec AC#12.</summary>
    public string HotKeysText { get; } =
        "Ctrl+1 Inventory · Ctrl+2 Node · Ctrl+3 Snapshots · Ctrl+4 Policies · " +
        "Ctrl+5 Operations · Ctrl+6 Drift · Ctrl+7 Audit · F5 Refresh inventory";

    public InventoryTreeViewModel Inventory { get; }

    public AddRouterWizardViewModel AddRouter { get; }

    public NodeDetailViewModel Node { get; }

    public SnapshotViewerViewModel Snapshot { get; }

    public SnapshotDiffViewModel Diff { get; }

    public ZonesViewModel Zones { get; }

    public PoliciesViewModel Policies { get; }

    public OnboardingViewModel Onboarding { get; }

    public DeploymentViewModel Deployment { get; }

    public DriftViewModel Drift { get; }

    public AuditViewModel Audit { get; }

    public RoutingAssuranceViewModel RoutingAssurance { get; }

    public string ControllerEndpoint => _options.ControllerEndpoint;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool IsInventorySelected => SelectedModule == ShellNavigationModule.Inventory;

    public bool IsNodeSelected => SelectedModule == ShellNavigationModule.Node;

    public bool IsSnapshotsSelected => SelectedModule == ShellNavigationModule.Snapshots;

    public bool IsPoliciesSelected => SelectedModule == ShellNavigationModule.Policies;

    public bool IsOperationsSelected => SelectedModule == ShellNavigationModule.Operations;

    public bool IsDriftSelected => SelectedModule == ShellNavigationModule.Drift;

    public bool IsAuditSelected => SelectedModule == ShellNavigationModule.Audit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private ControllerConnectionState _connectionState = ControllerConnectionState.Disconnected;

    [ObservableProperty]
    private string _statusText = "Disconnected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInventorySelected))]
    [NotifyPropertyChangedFor(nameof(IsNodeSelected))]
    [NotifyPropertyChangedFor(nameof(IsSnapshotsSelected))]
    [NotifyPropertyChangedFor(nameof(IsPoliciesSelected))]
    [NotifyPropertyChangedFor(nameof(IsOperationsSelected))]
    [NotifyPropertyChangedFor(nameof(IsDriftSelected))]
    [NotifyPropertyChangedFor(nameof(IsAuditSelected))]
    private ShellNavigationModule _selectedModule;

    [RelayCommand]
    private void SelectModule(ShellNavigationModule module) => SelectedModule = module;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        try
        {
            await Task.Run(async () => await _connection.ConnectAsync().ConfigureAwait(false))
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            await Task.Run(async () => await _connection.DisconnectAsync().ConfigureAwait(false))
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanConnect()
        => !IsBusy && ConnectionState is not (ControllerConnectionState.Connecting or ControllerConnectionState.Connected);

    private bool CanDisconnect()
        => !IsBusy && ConnectionState is not ControllerConnectionState.Disconnected;

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            SyncFromService();
        }
        else
        {
            Dispatcher.UIThread.Post(SyncFromService);
        }
    }

    private void SyncFromService()
    {
        ConnectionState = _connection.State;
        ErrorText = _connection.LastError;
        StatusText = DesktopConnectionStatusText.Format(ConnectionState, _options);
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _connection.StateChanged -= OnConnectionStateChanged;
        Audit.Dispose();
        RoutingAssurance.Dispose();
        Drift.Dispose();
        Policies.Dispose();
        Deployment.Dispose();
        Onboarding.Dispose();
        Zones.Dispose();
        Diff.Dispose();
        Snapshot.Dispose();
        Node.Dispose();
        AddRouter.Dispose();
        Inventory.Dispose();
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
