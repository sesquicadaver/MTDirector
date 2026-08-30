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
/// List stays compact; selection loads <c>GetDriftEvent</c> for the full payload.
/// No automatic fix / ForceRepair / AutoHeal commands (M6-04 AC#7).
/// </summary>
public sealed partial class DriftViewModel : ObservableObject, IDisposable
{
    private readonly IDriftServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private CancellationTokenSource? _loadCts;
    private int _detailEpoch;
    private bool _suppressDetailLoad;
    private bool _disposed;
    private bool _detailLoaded;
    private Guid? _detailEventId;
    private IReadOnlyList<DriftFindingListItem> _detailFindings = [];

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
    [NotifyPropertyChangedFor(nameof(SelectedEventFindings))]
    [NotifyPropertyChangedFor(nameof(HasSelectedEventFindings))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedEventFindings))]
    [NotifyPropertyChangedFor(nameof(HasSelectedEventDetail))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedEventDetail))]
    private DriftEventListItem? _selectedEvent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEventDetail))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedEventDetail))]
    private string _detailNodeIdText = string.Empty;

    [ObservableProperty]
    private string _detailBaselineHashText = string.Empty;

    [ObservableProperty]
    private string _detailActualHashText = string.Empty;

    [ObservableProperty]
    private string _detailDesiredHashText = string.Empty;

    [ObservableProperty]
    private string _detailSemanticDiffHashText = string.Empty;

    [ObservableProperty]
    private string _detailImmutableText = string.Empty;

    /// <summary>
    /// Findings from GetDriftEvent when loaded for the selected id; otherwise the ListDeviceDriftEvents row.
    /// </summary>
    public IReadOnlyList<DriftFindingListItem> SelectedEventFindings
    {
        get
        {
            if (_detailLoaded && _detailEventId is Guid id && SelectedEvent?.Id == id)
            {
                return _detailFindings;
            }

            return SelectedEvent?.Findings ?? [];
        }
    }

    public bool HasSelectedEventFindings => SelectedEventFindings.Count > 0;

    public bool HasNoSelectedEventFindings => SelectedEventFindings.Count == 0;

    public bool HasSelectedEventDetail =>
        _detailLoaded && _detailEventId is Guid id && SelectedEvent?.Id == id;

    public bool HasNoSelectedEventDetail => !HasSelectedEventDetail;

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

            _suppressDetailLoad = true;
            SelectedEvent = Events.FirstOrDefault();
            _suppressDetailLoad = false;
            StatusText = $"Loaded {Events.Count} drift event(s) for device {deviceId.Value:D}.";
            ErrorText = null;
            await LoadSelectedDetailAsync(token).ConfigureAwait(true);
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
        ApplyListSnapshot(value);
        if (_suppressDetailLoad)
        {
            return;
        }

        _ = LoadSelectedDetailAsync(CancellationToken.None);
    }

    private void ApplyListSnapshot(DriftEventListItem? value)
    {
        Interlocked.Increment(ref _detailEpoch);
        _detailLoaded = false;
        _detailEventId = null;
        _detailFindings = [];
        SemanticDiffText = value?.SemanticDiffCanonical ?? string.Empty;
        DetailNodeIdText = string.Empty;
        DetailBaselineHashText = string.Empty;
        DetailActualHashText = string.Empty;
        DetailDesiredHashText = string.Empty;
        DetailSemanticDiffHashText = string.Empty;
        DetailImmutableText = string.Empty;
        NotifyFindingsChanged();
    }

    private async Task LoadSelectedDetailAsync(CancellationToken cancellationToken)
    {
        DriftEventListItem? selected = SelectedEvent;
        if (selected is null)
        {
            return;
        }

        if (_connection.State != ControllerConnectionState.Connected)
        {
            return;
        }

        int epoch = Volatile.Read(ref _detailEpoch);
        Guid eventId = selected.Id;
        try
        {
            DriftEvent evt = await Task.Run(
                    async () => await _client.GetDriftEventAsync(eventId, cancellationToken).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(true);
            if (epoch != Volatile.Read(ref _detailEpoch) || SelectedEvent?.Id != eventId)
            {
                return;
            }

            ApplyGetPayload(evt);
            ErrorText = null;
        }
        catch (OperationCanceledException)
        {
            if (epoch != Volatile.Read(ref _detailEpoch))
            {
                return;
            }

            StatusText = "GetDriftEvent cancelled.";
        }
        catch (RpcException ex)
        {
            if (epoch != Volatile.Read(ref _detailEpoch))
            {
                return;
            }

            ErrorText = ex.Status.Detail;
            StatusText = "GetDriftEvent failed; showing list payload.";
        }
        catch (Exception ex)
        {
            if (epoch != Volatile.Read(ref _detailEpoch))
            {
                return;
            }

            ErrorText = ex.Message;
            StatusText = "GetDriftEvent failed; showing list payload.";
        }
    }

    private void ApplyGetPayload(DriftEvent evt)
    {
        _detailEventId = DesktopProtoUuid.ToGuid(evt.Id);
        _detailFindings = evt.Findings.Select(DriftFindingListItem.FromProto).ToArray();
        _detailLoaded = true;
        SemanticDiffText = evt.SemanticDiffCanonical ?? string.Empty;
        DetailNodeIdText = DesktopProtoUuid.ToGuid(evt.NodeId).ToString("D");
        DetailBaselineHashText = FormatHashFull(evt.BaselineCommittedHash);
        DetailActualHashText = FormatHashFull(evt.ActualManagedResourceHash);
        DetailDesiredHashText = FormatHashFull(evt.DesiredArtifactHashIgnoredForBaseline);
        DetailSemanticDiffHashText = FormatHashFull(evt.SemanticDiffHash);
        DetailImmutableText = evt.Immutable ? "immutable" : "mutable (unexpected)";
        NotifyFindingsChanged();
    }

    private void NotifyFindingsChanged()
    {
        OnPropertyChanged(nameof(SelectedEventFindings));
        OnPropertyChanged(nameof(HasSelectedEventFindings));
        OnPropertyChanged(nameof(HasNoSelectedEventFindings));
        OnPropertyChanged(nameof(HasSelectedEventDetail));
        OnPropertyChanged(nameof(HasNoSelectedEventDetail));
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

    private static string FormatHashFull(Sha256? hash)
    {
        if (hash is null || hash.Value.Length == 0)
        {
            return "—";
        }

        return Convert.ToHexString(hash.Value.Span).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Increment(ref _detailEpoch);
        _inventory.PropertyChanged -= OnInventoryPropertyChanged;
        _connection.StateChanged -= OnConnectionStateChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}

/// <summary>Presentation row for a drift event (Contracts-only). List hashes stay truncated.</summary>
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

    public required IReadOnlyList<DriftFindingListItem> Findings { get; init; }

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
            Findings = evt.Findings.Select(DriftFindingListItem.FromProto).ToArray(),
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

/// <summary>One DriftFinding from list or GetDriftEvent (kind / severity / detail).</summary>
public sealed class DriftFindingListItem
{
    public required string KindText { get; init; }

    public required string SeverityText { get; init; }

    public required string Detail { get; init; }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public string SummaryLine => HasDetail
        ? $"{SeverityText} · {KindText} · {Detail}"
        : $"{SeverityText} · {KindText}";

    public static DriftFindingListItem FromProto(DriftFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return new DriftFindingListItem
        {
            KindText = finding.Kind.ToString(),
            SeverityText = finding.Severity.ToString(),
            Detail = finding.Detail ?? string.Empty,
        };
    }
}
