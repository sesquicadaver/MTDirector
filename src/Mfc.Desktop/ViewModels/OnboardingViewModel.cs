using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Onboarding operator panel: checklist, placement preview, recovery facts, Watch progress.
/// No script source and no free-form RouterOS write controls.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject, IDisposable
{
    private readonly IOnboardingServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private bool _disposed;

    /// <summary>Resolved owner NodeId for PlanId/OperationId; cleared on cross-node selection.</summary>
    private Guid? _mutationOwnerNodeId;

    public OnboardingViewModel(
        IOnboardingServiceClient client,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _connection.StateChanged += OnConnectionStateChanged;
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
        RefreshTargetHint();
    }

    public ObservableCollection<OnboardingFindingListItem> Findings { get; } = [];

    public ObservableCollection<OnboardingPlacementListItem> Placements { get; } = [];

    public ObservableCollection<string> ProgressLines { get; } = [];

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasScriptSource
    {
        get
        {
            _ = _client;
            return false;
        }
    }

    public bool HasArbitraryWriteControls
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
    private string _statusText = "Select a Node, then validate prerequisites.";

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
    private async Task ValidateAsync()
    {
        await RunAsync(async () =>
        {
            InventoryNodeViewModel node = InventoryOpsSelection.RequireNode(
                _inventory.SelectedNode,
                _inventory.Roots);
            _ = InventoryOpsSelection.RequireDeviceIds(node);
            OnboardingPrerequisiteReport report = await _client.ValidatePrerequisitesAsync(
                node.Id,
                [],
                CancellationToken.None).ConfigureAwait(true);
            Findings.Clear();
            if (report.Findings.Count == 0 && !report.Passed)
            {
                Findings.Add(new OnboardingFindingListItem
                {
                    Code = "ONBOARDING_FACTS_REQUIRED",
                    Severity = "BLOCKER",
                    Message =
                        "Controller must supply capture-derived prerequisite facts; Desktop no longer fabricates DefaultFacts (AUDIT-GUI-01).",
                });
            }

            foreach (OnboardingFinding finding in report.Findings)
            {
                Findings.Add(new OnboardingFindingListItem
                {
                    Code = finding.Code,
                    Severity = finding.Severity.ToString(),
                    Message = finding.Message,
                });
            }

            StatusText = report.Passed ? "Prerequisites passed." : "Prerequisites have blockers.";
            if (!report.Passed && Findings.Count == 0)
            {
                StatusText = "Prerequisites blocked: empty facts are fail-closed (AUDIT-GUI-01).";
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreatePlanAsync()
    {
        await RunAsync(async () =>
        {
            InventoryNodeViewModel node = InventoryOpsSelection.RequireNode(
                _inventory.SelectedNode,
                _inventory.Roots);
            _ = InventoryOpsSelection.RequireDeviceIds(node);
            throw new InvalidOperationException(
                "Onboarding Create plan requires Controller-built device plans from last capture; Desktop no longer fabricates hashes (AUDIT-GUI-01).");
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
            StartWatchOutcome outcome = await Task.Run(
                    async () => await StartAndWatchAsync(planId, planHash, CancellationToken.None)
                        .ConfigureAwait(false),
                    CancellationToken.None)
                .ConfigureAwait(true);
            OperationId = DesktopProtoUuid.ToGuid(outcome.Started.OperationId);
            ProgressLines.Clear();
            IReadOnlyList<string> lines = outcome.WatchLines.Count > 0
                ? outcome.WatchLines
                : outcome.Started.Timeline;
            foreach (string line in lines)
            {
                ProgressLines.Add(line);
            }

            StatusText = $"Operation {outcome.LastState}.";
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
            OnboardingRecoveryStatus status = await _client.GetRecoveryStatusAsync(nodeId, OperationId, CancellationToken.None)
                .ConfigureAwait(true);
            RecoveryFactsText =
                $"state={status.OperationState}; action={status.Action}; node={status.NodeManagementState}; error={status.ErrorCode}";
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

    private async Task<StartWatchOutcome> StartAndWatchAsync(
        Guid planId,
        Sha256 planHash,
        CancellationToken cancellationToken)
    {
        OnboardingOperationSummary started = await _client.StartAsync(planId, planHash, cancellationToken)
            .ConfigureAwait(false);
        List<string> watchLines = [];
        OnboardingOperationState lastState = started.State;
        await foreach (OnboardingProgress progress in _client
                           .WatchAsync(DesktopProtoUuid.ToGuid(started.OperationId), cancellationToken)
                           .ConfigureAwait(false))
        {
            lastState = progress.State;
            watchLines.Add(FormatProgress(progress));
        }

        return new StartWatchOutcome(started, watchLines, lastState);
    }

    /// <summary>Rollback then Watch (off UI thread). Hub may replay Start+Rollback history after W6-04 hub fix.</summary>
    private async Task<RollbackWatchOutcome> RollbackAndWatchAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        OnboardingOperationSummary rolled = await _client.RollbackAsync(operationId, cancellationToken)
            .ConfigureAwait(false);
        List<string> watchLines = [];
        OnboardingOperationState lastState = rolled.State;
        await foreach (OnboardingProgress progress in _client.WatchAsync(operationId, cancellationToken)
                           .ConfigureAwait(false))
        {
            lastState = progress.State;
            watchLines.Add(FormatProgress(progress));
        }

        return new RollbackWatchOutcome(rolled, watchLines, lastState);
    }

    private static string FormatProgress(OnboardingProgress progress)
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
        Findings.Clear();
        Placements.Clear();
        ProgressLines.Clear();
        RecoveryFactsText = string.Empty;
    }

    private void RefreshTargetHint()
    {
        TargetHint = InventoryOpsSelection.FormatTargetHint(_inventory.SelectedNode, _inventory.Roots);
        HasVrrpPairTarget = InventoryOpsSelection.IsVrrpPair(_inventory.SelectedNode, _inventory.Roots);
    }

    private sealed record StartWatchOutcome(
        OnboardingOperationSummary Started,
        IReadOnlyList<string> WatchLines,
        OnboardingOperationState LastState);

    private sealed record RollbackWatchOutcome(
        OnboardingOperationSummary Rolled,
        IReadOnlyList<string> WatchLines,
        OnboardingOperationState LastState);
}
