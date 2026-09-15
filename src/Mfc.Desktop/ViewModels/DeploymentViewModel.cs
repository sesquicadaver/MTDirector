using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Deployment operator panel: typed semantic diff (kind/path/before/after), artifacts, order, probes/TTL, Watch progress (Start and Rollback), recovery.
/// Does not run SemanticDiffEngine locally. No ForceApply and no raw RouterOS command surface.
/// </summary>
public sealed partial class DeploymentViewModel : ObservableObject, IDisposable
{
    private readonly IDeploymentServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private readonly ISealedCompileDeployHandoffStore _sealedHandoff;
    private bool _disposed;

    /// <summary>Resolved owner NodeId for PlanId/OperationId; cleared on cross-node selection.</summary>
    private Guid? _mutationOwnerNodeId;

    /// <summary>Capture-derived packet-path pairs from last sealed CreatePlan; cleared on node switch.</summary>
    private DeploymentPacketPathPairFact[] _capturePacketPathPairs = [];

    public DeploymentViewModel(
        IDeploymentServiceClient client,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory,
        ISealedCompileDeployHandoffStore? sealedHandoff = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _sealedHandoff = sealedHandoff ?? new SealedCompileDeployHandoffStore();
        _connection.StateChanged += OnConnectionStateChanged;
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
        RefreshTargetHint();
    }

    public ObservableCollection<DeploymentSemanticDiffListItem> SemanticDiffRows { get; } = [];

    public ObservableCollection<string> SemanticDiffLines { get; } = [];

    public ObservableCollection<string> ArtifactLines { get; } = [];

    public ObservableCollection<string> OrderLines { get; } = [];

    public ObservableCollection<string> ProbeAndWatchdogLines { get; } = [];

    public ObservableCollection<string> ProgressLines { get; } = [];

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasForceApply
    {
        get
        {
            _ = _client;
            return false;
        }
    }

    public bool HasRawRouterOsCommands
    {
        get
        {
            _ = _inventory;
            return false;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Select a Node, then create a deployment plan.";

    [ObservableProperty]
    private string _targetHint = InventoryOpsSelection.FormatTargetHint(null, []);

    [ObservableProperty]
    private bool _hasVrrpPairTarget;

    [ObservableProperty]
    private string _recoveryFactsText = string.Empty;

    [ObservableProperty]
    private Guid? _planId;

    [ObservableProperty]
    private Sha256? _planHash;

    [ObservableProperty]
    private Guid? _operationId;

    [RelayCommand]
    private async Task CreatePlanAsync()
    {
        await RunAsync(async () =>
        {
            InventoryNodeViewModel node = InventoryOpsSelection.RequireNode(
                _inventory.SelectedNode,
                _inventory.Roots);
            Guid nodeId = node.Id;
            SealedCompileDeployHandoff handoff = _sealedHandoff.Current
                ?? throw new InvalidOperationException(
                    "Compile sealed artifacts on the Policies panel for this Node before Create plan.");
            if (handoff.NodeId != nodeId)
            {
                throw new InvalidOperationException(
                    "Sealed compile handoff belongs to another Node; re-compile for the selected Node.");
            }

            CreateDeploymentPlanFromSealedArtifactsResponse response = await _client
                .CreatePlanFromSealedArtifactsAsync(
                    nodeId,
                    handoff.AnalysisRunId,
                    handoff.Artifacts.Select(a => new SealedArtifactDeviceRef
                    {
                        DeviceId = DesktopProtoUuid.FromGuid(a.DeviceId),
                        NewArtifactResourceHash = a.ResourceHash,
                    }).ToList(),
                    CancellationToken.None)
                .ConfigureAwait(true);
            DeploymentPlanSummary plan = response.Plan
                ?? throw new InvalidOperationException("Controller returned an empty sealed deployment plan.");
            PlanId = DesktopProtoUuid.ToGuid(plan.PlanId);
            PlanHash = plan.PlanHash;
            _mutationOwnerNodeId = nodeId;
            _capturePacketPathPairs = response.CapturePacketPathPairs.ToArray();
            SemanticDiffRows.Clear();
            SemanticDiffLines.Clear();
            foreach (DeploymentSemanticDiffEntry entry in plan.SemanticDiff)
            {
                DeploymentSemanticDiffListItem row = DeploymentSemanticDiffListItem.FromProto(entry);
                SemanticDiffRows.Add(row);
                if (!string.Equals(row.HashDeltaText, "—", StringComparison.Ordinal))
                {
                    SemanticDiffLines.Add(row.HashDeltaText);
                }
            }

            if (SemanticDiffLines.Count == 0)
            {
                foreach (string entry in plan.SemanticDiffEntries)
                {
                    SemanticDiffLines.Add(entry);
                }
            }

            ArtifactLines.Clear();
            OrderLines.Clear();
            ProbeAndWatchdogLines.Clear();
            foreach (DeploymentDevicePlanView device in plan.Devices)
            {
                ArtifactLines.Add(
                    $"device={DesktopProtoUuid.ToGuid(device.DeviceId):D} old={ToHex(device.OldArtifactHash)} new={ToHex(device.NewArtifactHash)}");
                OrderLines.Add(
                    $"activation=[{string.Join(',', device.ActivationOrderMarkers)}] rollback=[{string.Join(',', device.RollbackOrderMarkers)}]");
                ProbeAndWatchdogLines.Add($"watchdog_ttl_seconds={device.WatchdogTtlSeconds}");
                foreach (DeploymentProbeView probe in device.Probes)
                {
                    ProbeAndWatchdogLines.Add($"probe:{probe.Kind}:{probe.Destination}:{probe.TimeoutMilliseconds}ms");
                }
            }

            foreach (Uuid deviceOrderId in plan.ActivationOrderDeviceIds)
            {
                OrderLines.Add($"plan_activation_device={DesktopProtoUuid.ToGuid(deviceOrderId):D}");
            }

            foreach (Uuid deviceOrderId in plan.RollbackOrderDeviceIds)
            {
                OrderLines.Add($"plan_rollback_device={DesktopProtoUuid.ToGuid(deviceOrderId):D}");
            }

            foreach (DeploymentPacketPathPairFact pair in _capturePacketPathPairs)
            {
                ProbeAndWatchdogLines.Add(
                    $"packet_path:{pair.IngressInterface}->{pair.EgressInterface}:{pair.PathClass}");
            }

            StatusText = $"Plan {PlanId:D} created from sealed compile artifacts.";
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        await RunAsync(async () =>
        {
            if (PlanId is not Guid planId || PlanHash is null)
            {
                throw new InvalidOperationException("Create a plan before start.");
            }

            Sha256 planHash = PlanHash;
            await StartAndWatchAsync(planId, planHash, CancellationToken.None).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RollbackAsync()
    {
        await RunAsync(async () =>
        {
            if (OperationId is not Guid operationId)
            {
                throw new InvalidOperationException("Start an operation before rollback.");
            }

            RollbackWatchOutcome outcome = await Task.Run(
                    async () => await RollbackAndWatchAsync(operationId, CancellationToken.None)
                        .ConfigureAwait(false),
                    CancellationToken.None)
                .ConfigureAwait(true);
            IReadOnlyList<string> lines = outcome.WatchLines.Count > 0
                ? outcome.WatchLines
                : outcome.Rolled.Timeline;
            if (outcome.WatchLines.Count > 0)
            {
                ProgressLines.Clear();
            }

            foreach (string line in lines)
            {
                ProgressLines.Add(line);
            }

            StatusText = $"Rollback {outcome.LastState}.";
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RecoveryAsync()
    {
        await RunAsync(async () =>
        {
            Guid nodeId = RequireNodeId();
            DeploymentRecoveryStatus status = await _client.GetRecoveryStatusAsync(nodeId, OperationId, CancellationToken.None)
                .ConfigureAwait(true);
            RecoveryFactsText =
                $"state={status.OperationState}; action={status.Action}; error={status.ErrorCode}";
            StatusText = "Recovery facts refreshed.";
        }).ConfigureAwait(true);
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
    }

    private Guid RequireNodeId()
        => InventoryOpsSelection.RequireNode(_inventory.SelectedNode, _inventory.Roots).Id;

    private async Task StartAndWatchAsync(
        Guid planId,
        Sha256 planHash,
        CancellationToken cancellationToken)
    {
        DeploymentOperationSummary started = await _client.StartAsync(
                planId,
                planHash,
                RequireCapturePacketPathPairs(),
                cancellationToken)
            .ConfigureAwait(true);
        // AUDIT-INT-01 §18: retain OperationId immediately after Start, before Watch can fail.
        OperationId = DesktopProtoUuid.ToGuid(started.OperationId);
        ProgressLines.Clear();

        DeploymentOperationState lastState = started.State;
        bool streamed = false;
        await foreach (DeploymentProgress progress in _client
                           .WatchAsync(OperationId.Value, cancellationToken)
                           .ConfigureAwait(true))
        {
            lastState = progress.State;
            ProgressLines.Add(FormatProgress(progress));
            streamed = true;
        }

        if (!streamed)
        {
            foreach (string entry in started.Timeline)
            {
                ProgressLines.Add(entry);
            }
        }

        StatusText = $"Operation {lastState}.";
    }

    /// <summary>Rollback then Watch (off UI thread). Hub may replay Start+Rollback history after CONT-01 hub fix.</summary>
    private async Task<RollbackWatchOutcome> RollbackAndWatchAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        DeploymentOperationSummary rolled = await _client.RollbackAsync(operationId, cancellationToken)
            .ConfigureAwait(false);
        List<string> watchLines = [];
        DeploymentOperationState lastState = rolled.State;
        await foreach (DeploymentProgress progress in _client.WatchAsync(operationId, cancellationToken)
                           .ConfigureAwait(false))
        {
            lastState = progress.State;
            watchLines.Add(FormatProgress(progress));
        }

        return new RollbackWatchOutcome(rolled, watchLines, lastState);
    }

    private static string FormatProgress(DeploymentProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        string state = progress.State.ToString();
        if (progress.HasTimelineEntry && !string.IsNullOrWhiteSpace(progress.TimelineEntry))
        {
            return $"{state}: {progress.TimelineEntry}";
        }

        if (progress.HasErrorCode && !string.IsNullOrWhiteSpace(progress.ErrorCode))
        {
            return $"{state}: {progress.ErrorCode}";
        }

        return state;
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ErrorText = null;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (RpcException ex)
        {
            ErrorText = ex.Status.Detail;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(() => StatusText = _connection.State.ToString());

    private void OnInventoryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InventoryTreeViewModel.SelectedNode))
        {
            SyncMutationOwnerFromSelection();
            if (Dispatcher.UIThread.CheckAccess())
            {
                RefreshTargetHint();
                StatusText = "Node selection changed.";
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    RefreshTargetHint();
                    StatusText = "Node selection changed.";
                });
            }
        }
    }

    /// <summary>
    /// AUDIT-CTX-01: when resolved owner NodeId changes, drop plan/operation mutation state.
    /// Same-owner Node↔Device reselection keeps PlanId/OperationId. First owner assignment does not wipe.
    /// </summary>
    private void SyncMutationOwnerFromSelection()
    {
        Guid? nextOwner = InventoryOpsSelection.TryResolveNode(_inventory.SelectedNode, _inventory.Roots)?.Id;
        if (nextOwner == _mutationOwnerNodeId)
        {
            return;
        }

        if (_mutationOwnerNodeId is not null)
        {
            InvalidateMutationContext();
        }

        _mutationOwnerNodeId = nextOwner;
    }

    private void InvalidateMutationContext()
    {
        PlanId = null;
        PlanHash = null;
        OperationId = null;
        SemanticDiffRows.Clear();
        SemanticDiffLines.Clear();
        ArtifactLines.Clear();
        OrderLines.Clear();
        ProbeAndWatchdogLines.Clear();
        ProgressLines.Clear();
        RecoveryFactsText = string.Empty;
        _capturePacketPathPairs = [];
    }

    private void RefreshTargetHint()
    {
        TargetHint = InventoryOpsSelection.FormatTargetHint(_inventory.SelectedNode, _inventory.Roots);
        HasVrrpPairTarget = InventoryOpsSelection.IsVrrpPair(_inventory.SelectedNode, _inventory.Roots);
        Guid? nodeId = InventoryOpsSelection.TryResolveNode(_inventory.SelectedNode, _inventory.Roots)?.Id;
        _sealedHandoff.InvalidateUnlessNode(nodeId);
    }

    private DeploymentPacketPathPairFact[] RequireCapturePacketPathPairs()
    {
        if (_capturePacketPathPairs.Length == 0)
        {
            throw new InvalidOperationException(
                "Start requires capture-derived packet-path pairs from sealed Create plan (no fabricated interfaces).");
        }

        return _capturePacketPathPairs;
    }

    private static string ToHex(Sha256 hash)
        => Convert.ToHexString(hash.Value.Span)[..12] + "…";

    private sealed record RollbackWatchOutcome(
        DeploymentOperationSummary Rolled,
        IReadOnlyList<string> WatchLines,
        DeploymentOperationState LastState);
}

/// <summary>Presentation row for typed deployment semantic diff (Contracts-only). Kind/path/before/after are distinct fields.</summary>
public sealed class DeploymentSemanticDiffListItem
{
    public required string SummaryLine { get; init; }

    public required string KindText { get; init; }

    public required string PathText { get; init; }

    public required string BeforeText { get; init; }

    public required string AfterText { get; init; }

    public required string HashDeltaText { get; init; }

    public static DeploymentSemanticDiffListItem FromProto(DeploymentSemanticDiffEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string kind = entry.Kind switch
        {
            DeploymentSemanticDiffKind.ArtifactUnchanged => "ARTIFACT_UNCHANGED",
            DeploymentSemanticDiffKind.ArtifactChanged => "ARTIFACT_CHANGED",
            _ => "UNSPECIFIED",
        };
        return new DeploymentSemanticDiffListItem
        {
            KindText = kind,
            PathText = OrDash(entry.Path),
            BeforeText = OrDash(entry.Before),
            AfterText = OrDash(entry.After),
            HashDeltaText = OrDash(entry.HashDelta),
            SummaryLine = $"{kind} {OrDash(entry.Path)}",
        };
    }

    private static string OrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
}
