using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Desktop semantic diff viewer: base/target selection + virtualized DiffEntry rows from CompareSnapshots.
/// Does not run SemanticDiffEngine locally.
/// </summary>
public sealed partial class SnapshotDiffViewModel : ObservableObject, IDisposable
{
    private readonly ISnapshotDiffService _diff;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public SnapshotDiffViewModel(
        ISnapshotDiffService diff,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _diff = diff ?? throw new ArgumentNullException(nameof(diff));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
        _connection.StateChanged += OnConnectionStateChanged;
    }

    public ObservableCollection<SnapshotCaptureListItem> Captures { get; } = [];

    public ObservableCollection<SnapshotDiffSectionGroup> SectionGroups { get; } = [];

    public ObservableCollection<SnapshotDiffEntryItem> VisibleEntries { get; } = [];

    public ObservableCollection<string> Warnings { get; } = [];

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isNoDifferences;

    [ObservableProperty]
    private string _statusText = "Select base and target captures, then Compare.";

    [ObservableProperty]
    private SnapshotCaptureListItem? _baseCapture;

    [ObservableProperty]
    private SnapshotCaptureListItem? _targetCapture;

    [ObservableProperty]
    private SnapshotDiffSectionGroup? _selectedSectionGroup;

    [ObservableProperty]
    private bool _showConfigurationOnly;

    [ObservableProperty]
    private bool _showObservationOnly;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    [RelayCommand(CanExecute = nameof(CanReloadCaptures))]
    private async Task ReloadCapturesAsync()
    {
        if (_inventory.SelectedNode is not { Kind: InventoryTreeKind.Device } device)
        {
            ErrorText = "Select a device to load captures for diff.";
            return;
        }

        await LoadCapturesInternalAsync(device.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanCompare))]
    private async Task CompareAsync()
    {
        if (BaseCapture is null || TargetCapture is null)
        {
            ErrorText = "Choose base and target captures.";
            return;
        }

        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller before comparing snapshots.";
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;
        IsLoading = true;
        CompareCommand.NotifyCanExecuteChanged();
        try
        {
            SnapshotDiffLoadResult result = await Task.Run(
                    async () => await _diff
                        .CompareAsync(BaseCapture.CaptureId, TargetCapture.CaptureId, token)
                        .ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            ApplyCompareResult(result);
        }
        catch (OperationCanceledException)
        {
            ErrorText = "Compare cancelled.";
        }
        finally
        {
            IsLoading = false;
            CompareCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanReloadCaptures()
        => !IsLoading
           && _connection.State == ControllerConnectionState.Connected
           && _inventory.SelectedNode?.Kind == InventoryTreeKind.Device;

    private bool CanCompare()
        => !IsLoading
           && _connection.State == ControllerConnectionState.Connected
           && BaseCapture is not null
           && TargetCapture is not null
           && BaseCapture.CaptureId != TargetCapture.CaptureId;

    private void OnInventoryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(InventoryTreeViewModel.SelectedNode)
            or nameof(InventoryTreeViewModel.HasSelection)))
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            HandleSelectionChanged();
        }
        else
        {
            Dispatcher.UIThread.Post(HandleSelectionChanged);
        }
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        void Notify()
        {
            ReloadCapturesCommand.NotifyCanExecuteChanged();
            CompareCommand.NotifyCanExecuteChanged();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Notify();
        }
        else
        {
            Dispatcher.UIThread.Post(Notify);
        }
    }

    private void HandleSelectionChanged()
    {
        ReloadCapturesCommand.NotifyCanExecuteChanged();
        CompareCommand.NotifyCanExecuteChanged();
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        if (selected is null || selected.Kind != InventoryTreeKind.Device)
        {
            _cts?.Cancel();
            _diff.Clear();
            ApplyCapturesResult(new SnapshotDiffLoadResult { Succeeded = false });
            StatusText = "Select a device with completed captures to compare.";
            return;
        }

        if (ReloadCapturesCommand.CanExecute(null))
        {
            ReloadCapturesCommand.Execute(null);
        }
    }

    private async Task LoadCapturesInternalAsync(Guid deviceId)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;
        IsLoading = true;
        ReloadCapturesCommand.NotifyCanExecuteChanged();
        CompareCommand.NotifyCanExecuteChanged();
        try
        {
            SnapshotDiffLoadResult result = await Task.Run(
                    async () => await _diff.LoadCapturesAsync(deviceId, token).ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            ApplyCapturesResult(result);
            if (result.Captures.Count >= 2)
            {
                BaseCapture = Captures[1];
                TargetCapture = Captures[0];
                CompareCommand.NotifyCanExecuteChanged();
            }
        }
        catch (OperationCanceledException)
        {
            ErrorText = "Load cancelled.";
        }
        finally
        {
            IsLoading = false;
            ReloadCapturesCommand.NotifyCanExecuteChanged();
            CompareCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnBaseCaptureChanged(SnapshotCaptureListItem? value)
        => CompareCommand.NotifyCanExecuteChanged();

    partial void OnTargetCaptureChanged(SnapshotCaptureListItem? value)
        => CompareCommand.NotifyCanExecuteChanged();

    partial void OnSelectedSectionGroupChanged(SnapshotDiffSectionGroup? value)
        => RebuildVisibleEntries();

    partial void OnShowConfigurationOnlyChanged(bool value)
        => RebuildVisibleEntries();

    partial void OnShowObservationOnlyChanged(bool value)
        => RebuildVisibleEntries();

    partial void OnErrorTextChanged(string? value)
        => OnPropertyChanged(nameof(HasError));

    private void ApplyCapturesResult(SnapshotDiffLoadResult result)
    {
        Captures.Clear();
        foreach (SnapshotCaptureListItem capture in result.Captures)
        {
            Captures.Add(capture);
        }

        SectionGroups.Clear();
        VisibleEntries.Clear();
        Warnings.Clear();
        IsNoDifferences = false;
        BaseCapture = null;
        TargetCapture = null;
        ErrorText = result.Error;
        StatusText = result.Captures.Count == 0
            ? "No completed captures."
            : $"{result.Captures.Count} completed capture(s) available.";
    }

    private void ApplyCompareResult(SnapshotDiffLoadResult result)
    {
        SectionGroups.Clear();
        foreach (SnapshotDiffSectionGroup group in result.SectionGroups)
        {
            SectionGroups.Add(group);
        }

        Warnings.Clear();
        foreach (string warning in result.Warnings)
        {
            Warnings.Add(warning);
        }

        IsNoDifferences = result.IsNoDifferences;
        ErrorText = result.Error;
        StatusText = result.IsNoDifferences
            ? "No differences"
            : $"{result.AllEntries.Count} change(s) across {result.SectionGroups.Count} section(s).";
        SelectedSectionGroup = SectionGroups.FirstOrDefault();
        RebuildVisibleEntries();
    }

    private void RebuildVisibleEntries()
    {
        IEnumerable<SnapshotDiffEntryItem> source = SelectedSectionGroup is null
            ? SectionGroups.SelectMany(g => g.Entries)
            : SelectedSectionGroup.Entries;

        if (ShowConfigurationOnly && !ShowObservationOnly)
        {
            source = source.Where(e =>
                e.DomainText.Contains("Configuration", StringComparison.OrdinalIgnoreCase));
        }
        else if (ShowObservationOnly && !ShowConfigurationOnly)
        {
            source = source.Where(e =>
                e.DomainText.Contains("Observation", StringComparison.OrdinalIgnoreCase));
        }

        VisibleEntries.Clear();
        foreach (SnapshotDiffEntryItem entry in source)
        {
            VisibleEntries.Add(entry);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _inventory.PropertyChanged -= OnInventoryPropertyChanged;
        _connection.StateChanged -= OnConnectionStateChanged;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
