using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Drift module: list/show immutable drift events + semantic diff.
/// No automatic fix / ForceRepair / AutoHeal commands (M6-04 AC#7).
/// </summary>
public sealed partial class DriftViewModel : ObservableObject, IDisposable
{
    private readonly IDriftServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private CancellationTokenSource? _loadCts;
    private bool _disposed;

    public DriftViewModel(
        IDriftServiceClient client,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _connection.StateChanged += OnConnectionStateChanged;
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
    }

    public ObservableCollection<DriftEventListItem> Events { get; } = [];

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool IsCached
    {
        get
        {
            _ = _client;
            return false;
        }
    }

    public string CachedBadgeText => IsCached ? "Cached" : string.Empty;

    /// <summary>Living Spec / AC#7: no automatic repair surface.</summary>
    public bool HasAutomaticFix
    {
        get
        {
            _ = _inventory;
            return false;
        }
    }

    public bool HasForceRepairCommand
    {
        get
        {
            _ = _connection;
            return false;
        }
    }

    public bool HasAutoHealCommand
    {
        get
        {
            _ = Events;
            return false;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Select a Device, then refresh drift events.";

    [ObservableProperty]
    private string _semanticDiffText = string.Empty;

    [ObservableProperty]
    private DriftEventListItem? _selectedEvent;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller before loading drift events.";
            return;
        }

        Guid? deviceId = ResolveDeviceId();
        if (deviceId is null)
        {
            ErrorText = "Select a Device in the inventory tree.";
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        CancellationToken token = _loadCts.Token;

        IsBusy = true;
        RefreshCommand.NotifyCanExecuteChanged();
        try
        {
            IReadOnlyList<DriftEvent> events = await Task.Run(
                    async () => await _client.ListDeviceDriftEventsAsync(deviceId.Value, token).ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            Events.Clear();
            foreach (DriftEvent evt in events)
            {
                Events.Add(DriftEventListItem.FromProto(evt));
            }

            SelectedEvent = Events.FirstOrDefault();
            StatusText = $"Loaded {Events.Count} drift event(s) for device {deviceId.Value:D}.";
            ErrorText = null;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Drift refresh cancelled.";
        }
        catch (RpcException ex)
        {
            ErrorText = ex.Status.Detail;
            StatusText = "Drift load failed.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Drift load failed.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRefresh()
        => !IsBusy && _connection.State == ControllerConnectionState.Connected;

    partial void OnSelectedEventChanged(DriftEventListItem? value)
    {
        SemanticDiffText = value?.SemanticDiffCanonical ?? string.Empty;
    }

    private Guid? ResolveDeviceId()
    {
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        if (selected?.Kind == InventoryTreeKind.Device)
        {
            return selected.Id;
        }

        return null;
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshCommand.NotifyCanExecuteChanged();
        }
        else
        {
            Dispatcher.UIThread.Post(() => RefreshCommand.NotifyCanExecuteChanged());
        }
    }

    private void OnInventoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InventoryTreeViewModel.SelectedNode))
        {
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.StateChanged -= OnConnectionStateChanged;
        _inventory.PropertyChanged -= OnInventoryPropertyChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}

/// <summary>Presentation row for a drift event (Contracts-only).</summary>
public sealed class DriftEventListItem
{
    public required Guid Id { get; init; }

    public required string SummaryLine { get; init; }

    public required string OutcomeText { get; init; }

    public required string BaselineHashText { get; init; }

    public required string ActualHashText { get; init; }

    public required string SemanticDiffCanonical { get; init; }

    public required bool BlocksDeployment { get; init; }

    public required bool ConfigurationDriftPresent { get; init; }

    public static DriftEventListItem FromProto(DriftEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        Guid id = DesktopProtoUuid.ToGuid(evt.Id);
        return new DriftEventListItem
        {
            Id = id,
            OutcomeText = evt.Outcome.ToString(),
            BaselineHashText = FormatHash(evt.BaselineCommittedHash),
            ActualHashText = FormatHash(evt.ActualManagedResourceHash),
            SemanticDiffCanonical = evt.SemanticDiffCanonical ?? string.Empty,
            BlocksDeployment = evt.BlocksDeployment,
            ConfigurationDriftPresent = evt.ConfigurationDriftPresent,
            SummaryLine =
                $"{evt.CreatedAt?.ToDateTimeOffset():u} · {evt.Outcome} · " +
                $"config={evt.ConfigurationDriftPresent} · blocks={evt.BlocksDeployment} · " +
                $"findings={evt.Findings.Count}",
        };
    }

    private static string FormatHash(Sha256? hash)
    {
        if (hash is null || hash.Value.Length == 0)
        {
            return "—";
        }

        string hex = Convert.ToHexString(hash.Value.Span).ToLowerInvariant();
        return hex.Length <= 12 ? hex : hex[..12] + "…";
    }
}
