using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Audit module: read-only newest-first event list (M6-04 AC#8). No write commands.
/// </summary>
public sealed partial class AuditViewModel : ObservableObject, IDisposable
{
    private readonly IAuditServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private CancellationTokenSource? _loadCts;
    private bool _disposed;

    public AuditViewModel(IAuditServiceClient client, IControllerConnectionService connection)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.StateChanged += OnConnectionStateChanged;
    }

    public ObservableCollection<AuditEventListItem> Events { get; } = [];

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

    /// <summary>Living Spec / AC#8: no mutate surface on Desktop audit module.</summary>
    public bool IsReadOnly
    {
        get
        {
            _ = _connection;
            return true;
        }
    }

    public bool HasWriteCommands
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
    private string _statusText = "Connect and refresh to load audit events.";

    [ObservableProperty]
    private AuditEventListItem? _selectedEvent;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller before loading audit events.";
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
            IReadOnlyList<AuditEvent> events = await Task.Run(
                    async () => await _client.ListAuditEventsAsync(100, token).ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            Events.Clear();
            foreach (AuditEvent evt in events)
            {
                Events.Add(AuditEventListItem.FromProto(evt));
            }

            SelectedEvent = Events.FirstOrDefault();
            StatusText = $"Loaded {Events.Count} audit event(s) (newest first).";
            ErrorText = null;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Audit refresh cancelled.";
        }
        catch (RpcException ex)
        {
            ErrorText = ex.Status.Detail;
            StatusText = "Audit load failed.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Audit load failed.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRefresh()
        => !IsBusy && _connection.State == ControllerConnectionState.Connected;

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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.StateChanged -= OnConnectionStateChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}

/// <summary>Presentation row for an audit event (Contracts-only).</summary>
public sealed class AuditEventListItem
{
    public required Guid Id { get; init; }

    public required string SummaryLine { get; init; }

    public required string Actor { get; init; }

    public required string Action { get; init; }

    public required string PayloadJson { get; init; }

    public static AuditEventListItem FromProto(AuditEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        Guid id = DesktopProtoUuid.ToGuid(evt.Id);
        return new AuditEventListItem
        {
            Id = id,
            Actor = evt.Actor,
            Action = evt.Action,
            PayloadJson = evt.PayloadJson,
            SummaryLine = $"{evt.OccurredAt?.ToDateTimeOffset():u} · {evt.Actor} · {evt.Action}",
        };
    }
}
