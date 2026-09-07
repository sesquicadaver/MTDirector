using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Incident operator surface (DESK-INCIDENT-01…03): SEC-06 Ingest + BindAssessment — Contracts-only.
/// No deploy/overlay/feedback Desktop RPCs. MainWindow Operations → Incident tab binds this VM.
/// </summary>
public sealed partial class IncidentViewModel : ObservableObject, IDisposable
{
    private readonly IIncidentServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private IngestIncidentSignalRequest? _lastIngestRequest;
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

    public bool HasLastBinding => LastBinding is not null;

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

    [ObservableProperty]
    private string _endpointIdText = string.Empty;

    [ObservableProperty]
    private string _presenceIdText = string.Empty;

    [ObservableProperty]
    private string _enforcementNodeIdText = string.Empty;

    [ObservableProperty]
    private string _flowSourceAddress = string.Empty;

    [ObservableProperty]
    private string _flowDestinationAddress = string.Empty;

    [ObservableProperty]
    private string _flowProtocol = "tcp";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastBinding))]
    private IncidentAssessmentBindingListItem? _lastBinding;

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
            _lastIngestRequest = CloneIngestRequest(request, DesktopProtoUuid.ToGuid(signal.EventId));
            LastSignal = IncidentSignalListItem.FromProto(signal);
            LastBinding = null;
            StatusText = $"Ingested signal {LastSignal.EventIdText} ({LastSignal.Category}). Bind assessment when ready.";
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

    [RelayCommand(CanExecute = nameof(CanBind))]
    private async Task BindAssessmentAsync()
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller before binding incident assessment.";
            return;
        }

        if (LastSignal is null || _lastIngestRequest is null)
        {
            ErrorText = "Ingest a signal before binding assessment.";
            return;
        }

        if (!Guid.TryParse(EndpointIdText.Trim(), out Guid endpointId)
            || !Guid.TryParse(PresenceIdText.Trim(), out Guid presenceId)
            || !Guid.TryParse(EnforcementNodeIdText.Trim(), out Guid enforcementNodeId))
        {
            ErrorText = "Endpoint, presence, and enforcement node ids must be valid UUIDs.";
            return;
        }

        IngestIncidentSignalRequest signal = CloneIngestRequest(_lastIngestRequest, LastSignal.EventId);
        string flowSource = FlowSourceAddress.Trim();
        string flowDest = FlowDestinationAddress.Trim();
        string flowProto = FlowProtocol.Trim();
        if (!string.IsNullOrWhiteSpace(flowSource)
            && !string.IsNullOrWhiteSpace(flowDest)
            && !string.IsNullOrWhiteSpace(flowProto))
        {
            signal.Flow = new IncidentFlowTuple
            {
                SourceAddress = flowSource,
                DestinationAddress = flowDest,
                Protocol = flowProto,
            };
        }

        BindIncidentResponseAssessmentRequest request = new()
        {
            Signal = signal,
            EndpointId = DesktopProtoUuid.FromGuid(endpointId),
            PresenceId = DesktopProtoUuid.FromGuid(presenceId),
            EnforcementNodeId = DesktopProtoUuid.FromGuid(enforcementNodeId),
            AssessedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            SessionVisibility = IncidentSessionVisibilityStatus.Full,
            PacketPathClass = ObservedPacketPathClass.CpuFirewall,
        };

        IsBusy = true;
        NotifyCommands();
        ErrorText = null;
        try
        {
            IncidentResponseAssessmentBinding binding = await Task.Run(
                    async () => await _client.BindIncidentResponseAssessmentAsync(request, CancellationToken.None)
                        .ConfigureAwait(false),
                    CancellationToken.None)
                .ConfigureAwait(true);
            LastBinding = IncidentAssessmentBindingListItem.FromProto(binding);
            StatusText =
                $"Bound assessment for incident {LastBinding.IncidentIdText} ({LastBinding.FeasibilityText}).";
        }
        catch (RpcException ex)
        {
            ErrorText = ex.Status.Detail;
            StatusText = "Incident assessment bind failed.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Incident assessment bind failed.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private bool CanOperate()
        => !IsBusy && _connection.State == ControllerConnectionState.Connected;

    private bool CanBind()
        => CanOperate() && LastSignal is not null && _lastIngestRequest is not null;

    private void NotifyCommands()
    {
        IngestCommand.NotifyCanExecuteChanged();
        BindAssessmentCommand.NotifyCanExecuteChanged();
    }

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

    private static IngestIncidentSignalRequest CloneIngestRequest(IngestIncidentSignalRequest source, Guid eventId)
    {
        IngestIncidentSignalRequest clone = new()
        {
            EventId = DesktopProtoUuid.FromGuid(eventId),
            SourceEventId = source.SourceEventId,
            OccurredAt = source.OccurredAt,
            ReceivedAt = source.ReceivedAt,
            SourceType = source.SourceType,
            Category = source.Category,
            Severity = source.Severity,
            Confidence = source.Confidence,
            DeduplicationKey = source.DeduplicationKey,
        };
        if (source.Flow is not null)
        {
            clone.Flow = source.Flow.Clone();
        }

        return clone;
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

/// <summary>Presentation row for BindIncidentResponseAssessment result (Contracts-only).</summary>
public sealed class IncidentAssessmentBindingListItem
{
    public required Guid IncidentId { get; init; }

    public required string IncidentIdText { get; init; }

    public required string FeasibilityText { get; init; }

    public required string VisibilityStatusText { get; init; }

    public required int Confidence { get; init; }

    public required string StatusText { get; init; }

    public required string CorrelationFlowText { get; init; }

    public string SummaryLine =>
        $"{FeasibilityText} · visibility={VisibilityStatusText} · confidence={Confidence} · {StatusText}";

    public static IncidentAssessmentBindingListItem FromProto(IncidentResponseAssessmentBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        Guid incidentId = DesktopProtoUuid.ToGuid(binding.IncidentId);
        ResponseAssessment? assessment = binding.Assessment;
        IncidentFlowTuple? flow = binding.CorrelationFlow;
        string flowText = flow is null
            ? "—"
            : $"{flow.Protocol} {flow.SourceAddress} → {flow.DestinationAddress}";
        return new IncidentAssessmentBindingListItem
        {
            IncidentId = incidentId,
            IncidentIdText = incidentId.ToString("D"),
            FeasibilityText = assessment?.Feasibility ?? "—",
            VisibilityStatusText = assessment?.VisibilityStatus ?? "—",
            Confidence = assessment?.Confidence ?? 0,
            StatusText = assessment?.Status ?? "—",
            CorrelationFlowText = flowText,
        };
    }
}
