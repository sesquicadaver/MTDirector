using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>Shell view-model: connection, inventory, snapshot, diff, zones, policies, and onboarding.</summary>
public sealed partial class ShellViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public ShellViewModel(
        IControllerConnectionService connection,
        DesktopOptions options,
        InventoryTreeViewModel inventory,
        SnapshotViewerViewModel snapshot,
        SnapshotDiffViewModel diff,
        ZonesViewModel zones,
        PoliciesViewModel policies,
        OnboardingViewModel onboarding)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Diff = diff ?? throw new ArgumentNullException(nameof(diff));
        Zones = zones ?? throw new ArgumentNullException(nameof(zones));
        Policies = policies ?? throw new ArgumentNullException(nameof(policies));
        Onboarding = onboarding ?? throw new ArgumentNullException(nameof(onboarding));
        _connection.StateChanged += OnConnectionStateChanged;
        SyncFromService();
    }

    public InventoryTreeViewModel Inventory { get; }

    public SnapshotViewerViewModel Snapshot { get; }

    public SnapshotDiffViewModel Diff { get; }

    public ZonesViewModel Zones { get; }

    public PoliciesViewModel Policies { get; }

    public OnboardingViewModel Onboarding { get; }

    public string ControllerEndpoint => _options.ControllerEndpoint;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

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
        StatusText = ConnectionState switch
        {
            ControllerConnectionState.Connecting => "Connecting",
            ControllerConnectionState.Connected => "Connected",
            ControllerConnectionState.Disconnected => "Disconnected",
            ControllerConnectionState.AuthenticationFailed => "AuthenticationFailed",
            ControllerConnectionState.TlsError => "TlsError",
            _ => ConnectionState.ToString(),
        };
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
        Policies.Dispose();
        Onboarding.Dispose();
        Zones.Dispose();
        Diff.Dispose();
        Snapshot.Dispose();
        Inventory.Dispose();
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
