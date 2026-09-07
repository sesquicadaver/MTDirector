using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Incident operator surface (DESK-INCIDENT-01): SEC-06 Ingest (+ Bind client) — Contracts-only.
/// No deploy/overlay/feedback Desktop RPCs. MainWindow panel bindings land in DESK-INCIDENT-02.
/// </summary>
public sealed partial class IncidentViewModel : ObservableObject, IDisposable
{
    private readonly IIncidentServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private bool _disposed;

    public IncidentViewModel(IIncidentServiceClient client, IControllerConnectionService connection)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.StateChanged += OnConnectionStateChanged;
    }

    /// <summary>Living Spec: Desktop stays fail-closed beyond SEC-06 Ingest/Bind wire.</summary>
    public bool HasDeployOverlayCommands
    {
        get
        {
            _ = _client;
            return false;
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasLastSignal => LastSignal is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Connect, fill a minimal signal, then ingest.";

    [ObservableProperty]
    private string _sourceEventId = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _deduplicationKey = string.Empty;

    [ObservableProperty]
    private int _confidence = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastSignal))]
    private IncidentSignalListItem? _lastSignal;

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task IngestAsync()
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller before ingesting incident signals.";
            return;
        }

        string sourceEventId = SourceEventId.Trim();
        string category = Category.Trim();
        string dedup = DeduplicationKey.Trim();
        if (string.IsNullOrWhiteSpace(sourceEventId)
            || string.IsNullOrWhiteSpace(category)
            || string.IsNullOrWhiteSpace(dedup))
        {
            ErrorText = "Source event id, category, and deduplication key are required.";
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        IngestIncidentSignalRequest request = new()
        {
            EventId = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            SourceEventId = sourceEventId,
            OccurredAt = Timestamp.FromDateTimeOffset(now),
            ReceivedAt = Timestamp.FromDateTimeOffset(now),
            SourceType = IncidentSignalSourceType.Siem,
            Category = category,
            Severity = IncidentSeverity.High,
            Confidence = Confidence,
            DeduplicationKey = dedup,
        };

        IsBusy = true;
        NotifyCommands();
        ErrorText = null;
        try
        {
            IncidentSignal signal = await Task.Run(
                    async () => await _client.IngestIncidentSignalAsync(request, CancellationToken.None)
                        .ConfigureAwait(false),
                    CancellationToken.None)
                .ConfigureAwait(true);
            LastSignal = IncidentSignalListItem.FromProto(signal);
            StatusText = $"Ingested signal {LastSignal.EventIdText} ({LastSignal.Category}).";
        }
        catch (RpcException ex)
        {
            ErrorText = ex.Status.Detail;
            StatusText = "Incident ingest failed.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Incident ingest failed.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private bool CanOperate()
        => !IsBusy && _connection.State == ControllerConnectionState.Connected;

    private void NotifyCommands() => IngestCommand.NotifyCanExecuteChanged();

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            NotifyCommands();
        }
        else
        {
            Dispatcher.UIThread.Post(NotifyCommands);
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
    }
}

/// <summary>Presentation row for an ingested IncidentSignal (Contracts-only).</summary>
public sealed class IncidentSignalListItem
{
    public required Guid EventId { get; init; }

    public required string EventIdText { get; init; }

    public required string SourceEventId { get; init; }

    public required string Category { get; init; }

    public required string SeverityText { get; init; }

    public required string SourceTypeText { get; init; }

    public required int Confidence { get; init; }

    public required string DeduplicationKey { get; init; }

    public string SummaryLine =>
        $"{SeverityText} · {Category} · source={SourceEventId} · confidence={Confidence}";

    public static IncidentSignalListItem FromProto(IncidentSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        Guid id = DesktopProtoUuid.ToGuid(signal.EventId);
        return new IncidentSignalListItem
        {
            EventId = id,
            EventIdText = id.ToString("D"),
            SourceEventId = signal.SourceEventId ?? string.Empty,
            Category = signal.Category ?? string.Empty,
            SeverityText = signal.Severity.ToString(),
            SourceTypeText = signal.SourceType.ToString(),
            Confidence = signal.Confidence,
            DeduplicationKey = signal.DeduplicationKey ?? string.Empty,
        };
    }
}
