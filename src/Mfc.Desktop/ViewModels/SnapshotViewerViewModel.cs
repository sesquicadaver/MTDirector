using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Snapshot viewer: sections, config vs observations, hashes, schema version, and device capture.
/// Network work stays off the UI thread; unknown properties require technical view.
/// Record lists remain read-only; StartCapture is Controller snapshot persist (not Desktop→RouterOS write).
/// </summary>
public sealed partial class SnapshotViewerViewModel : ObservableObject, IDisposable
{
    private readonly ISnapshotViewerService _viewer;
    private readonly ISnapshotViewerClient _client;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _captureCts;
    private bool _disposed;
    private bool _suppressSelectionHandlers;

    public SnapshotViewerViewModel(
        ISnapshotViewerService viewer,
        ISnapshotViewerClient client,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
        _connection.StateChanged += OnConnectionStateChanged;
        RefreshPairGuidance();
    }

    public ObservableCollection<SnapshotCaptureListItem> Captures { get; } = [];

    public ObservableCollection<SnapshotSectionListItem> VisibleSections { get; } = [];

    public ObservableCollection<SnapshotRecordListItem> ConfigurationRecords { get; } = [];

    public ObservableCollection<SnapshotRecordListItem> ObservationRecords { get; } = [];

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySanitizedCommand))]
    [NotifyCanExecuteChangedFor(nameof(CaptureCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySanitizedCommand))]
    [NotifyCanExecuteChangedFor(nameof(CaptureCommand))]
    private bool _isCapturing;

    [ObservableProperty]
    private bool _showTechnicalView;

    [ObservableProperty]
    private string _statusText = "—";

    [ObservableProperty]
    private string _captureProgressText = "—";

    [ObservableProperty]
    private string _schemaVersionText = "—";

    [ObservableProperty]
    private string _configurationHashText = "—";

    [ObservableProperty]
    private string _observationHashText = "—";

    [ObservableProperty]
    private string _capabilityHashText = "—";

    [ObservableProperty]
    private string _completedAtText = "—";

    [ObservableProperty]
    private string _selectedSectionId = string.Empty;

    [ObservableProperty]
    private SnapshotCaptureListItem? _selectedCapture;

    [ObservableProperty]
    private SnapshotSectionListItem? _selectedSection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedRecordDetail))]
    [NotifyPropertyChangedFor(nameof(SelectedRecordFields))]
    [NotifyPropertyChangedFor(nameof(HasSelectedRecord))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedRecord))]
    private SnapshotRecordListItem? _selectedConfigurationRecord;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedRecordDetail))]
    [NotifyPropertyChangedFor(nameof(SelectedRecordFields))]
    [NotifyPropertyChangedFor(nameof(HasSelectedRecord))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedRecord))]
    private SnapshotRecordListItem? _selectedObservationRecord;

    [ObservableProperty]
    private string _hintText = "Select a device in inventory to view its latest completed snapshot.";

    [ObservableProperty]
    private string _pairGuidanceText = string.Empty;

    [ObservableProperty]
    private bool _hasVrrpPairGuidance;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasCapture => !string.IsNullOrWhiteSpace(SelectedSectionId) || Captures.Count > 0;

    /// <summary>The last selected configuration or observation record (mutually exclusive).</summary>
    public SnapshotRecordListItem? SelectedRecordDetail =>
        SelectedConfigurationRecord ?? SelectedObservationRecord;

    public IReadOnlyList<SnapshotFieldLine> SelectedRecordFields =>
        SelectedRecordDetail?.Fields ?? [];

    public bool HasSelectedRecord => SelectedRecordDetail is not null;

    public bool HasNoSelectedRecord => SelectedRecordDetail is null;

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task ReloadAsync()
    {
        if (_inventory.SelectedNode is not { Kind: InventoryTreeKind.Device } device)
        {
            ErrorText = "Select a device to load snapshots.";
            return;
        }

        await LoadDeviceInternalAsync(device.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private async Task CaptureAsync()
    {
        if (_inventory.SelectedNode is not { Kind: InventoryTreeKind.Device } device)
        {
            ErrorText = InventoryOpsSelection.IsVrrpPair(_inventory.SelectedNode, _inventory.Roots)
                ? "Select a VRRP member Device to capture (not the Node)."
                : "Select a device to capture.";
            return;
        }

        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller before capturing.";
            return;
        }

        _captureCts?.Cancel();
        _captureCts?.Dispose();
        _captureCts = new CancellationTokenSource();
        CancellationToken token = _captureCts.Token;
        Guid deviceId = device.Id;
        IsCapturing = true;
        ErrorText = null;
        CaptureProgressText = "Starting capture…";
        try
        {
            CaptureRunOutcome outcome = await Task.Run(
                    async () => await RunCaptureAsync(deviceId, token).ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            if (outcome.ProgressLines.Count > 0)
            {
                CaptureProgressText = outcome.ProgressLines[^1];
            }

            if (outcome.LastProgress?.Stage == CaptureStage.Failed)
            {
                ErrorText = outcome.LastProgress.Error?.SanitizedDetail ?? "Capture failed.";
                return;
            }

            if (outcome.LastProgress?.Stage == CaptureStage.Canceled)
            {
                ErrorText = "Capture cancelled.";
                return;
            }

            await LoadDeviceInternalAsync(deviceId).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            CaptureProgressText = "Canceled";
            ErrorText = "Capture cancelled.";
        }
        catch (RpcException ex)
        {
            CaptureProgressText = "Failed";
            ErrorText = ex.Status.Detail;
        }
        catch (Exception ex)
        {
            CaptureProgressText = "Failed";
            ErrorText = ex.Message;
        }
        finally
        {
            IsCapturing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopySanitizedAsync()
    {
        string text = SnapshotViewerService.BuildSanitizedExport(
            BuildExportState(),
            ShowTechnicalView);
        IClipboard? clipboard = GetClipboard();
        if (clipboard is null)
        {
            ErrorText = "Clipboard is not available.";
            return;
        }

        await clipboard.SetTextAsync(text).ConfigureAwait(true);
    }

    private bool CanReload()
        => !IsLoading
           && !IsCapturing
           && _connection.State == ControllerConnectionState.Connected
           && _inventory.SelectedNode?.Kind == InventoryTreeKind.Device;

    private bool CanCapture()
        => !IsLoading
           && !IsCapturing
           && _connection.State == ControllerConnectionState.Connected
           && _inventory.SelectedNode?.Kind == InventoryTreeKind.Device;

    private bool CanCopy()
        => !IsLoading && !IsCapturing && Captures.Count > 0;

    private async Task<CaptureRunOutcome> RunCaptureAsync(Guid deviceId, CancellationToken token)
    {
        StartCaptureResponse started = await _client
            .StartCaptureAsync(deviceId, Guid.NewGuid(), token)
            .ConfigureAwait(false);
        List<string> lines = [];
        CaptureProgress? last = null;
        await foreach (CaptureProgress progress in _client
                           .WatchCaptureAsync(DesktopProtoUuid.ToGuid(started.OperationId), token)
                           .ConfigureAwait(false))
        {
            last = progress;
            lines.Add(FormatCaptureProgress(progress));
        }

        return new CaptureRunOutcome(last, lines);
    }

    private static string FormatCaptureProgress(CaptureProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        string stage = progress.Stage.ToString();
        if (progress.HasCurrentSection && !string.IsNullOrWhiteSpace(progress.CurrentSection))
        {
            return $"{stage}: {progress.CurrentSection}";
        }

        if (progress.Error is ErrorDetail error && !string.IsNullOrWhiteSpace(error.SanitizedDetail))
        {
            return $"{stage}: {error.SanitizedDetail}";
        }

        return stage;
    }

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
        if (Dispatcher.UIThread.CheckAccess())
        {
            NotifyCaptureCommands();
        }
        else
        {
            Dispatcher.UIThread.Post(NotifyCaptureCommands);
        }
    }

    private void NotifyCaptureCommands()
    {
        ReloadCommand.NotifyCanExecuteChanged();
        CaptureCommand.NotifyCanExecuteChanged();
        CopySanitizedCommand.NotifyCanExecuteChanged();
    }

    private void HandleSelectionChanged()
    {
        NotifyCaptureCommands();
        RefreshPairGuidance();
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        if (selected is null || selected.Kind != InventoryTreeKind.Device)
        {
            _loadCts?.Cancel();
            _captureCts?.Cancel();
            _viewer.Clear();
            ApplyResult(new SnapshotViewerLoadResult { Succeeded = false });
            HintText = "Select a device in inventory to view its latest completed snapshot.";
            RefreshPairGuidance();
            return;
        }

        if (ReloadCommand.CanExecute(null))
        {
            ReloadCommand.Execute(null);
        }
    }

    private void RefreshPairGuidance()
    {
        PairGuidanceText = InventoryOpsSelection.FormatCaptureGuidance(
            _inventory.SelectedNode,
            _inventory.Roots);
        HasVrrpPairGuidance = !string.IsNullOrWhiteSpace(PairGuidanceText);
    }

    private async Task LoadDeviceInternalAsync(Guid deviceId)
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller before loading snapshots.";
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        CancellationToken token = _loadCts.Token;
        IsLoading = true;
        try
        {
            SnapshotViewerLoadResult result = await Task.Run(
                    async () => await _viewer.LoadDeviceAsync(deviceId, token).ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            ApplyResult(result);
            if (result is { Succeeded: true, CaptureId: Guid captureId, Sections.Count: > 0 })
            {
                SnapshotSectionListItem? preferred = SnapshotPresentationIdentity
                    .PreferOperatorFacingSection(VisibleSections.ToArray());
                if (preferred is not null)
                {
                    SelectedSection = preferred;
                    await LoadSelectedSectionAsync(captureId, preferred.SectionId, token)
                        .ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            ApplyResult(new SnapshotViewerLoadResult
            {
                Succeeded = false,
                Error = "Load cancelled.",
                DeviceId = deviceId,
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSelectedSectionAsync(
        Guid captureId,
        string sectionId,
        CancellationToken token)
    {
        IsLoading = true;
        try
        {
            SnapshotViewerLoadResult result = await Task.Run(
                    async () => await _viewer.LoadSectionAsync(captureId, sectionId, token)
                        .ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            ApplySectionRecords(result);
            ErrorText = result.Error;
        }
        catch (OperationCanceledException)
        {
            ErrorText = "Section load cancelled.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedCaptureChanged(SnapshotCaptureListItem? value)
    {
        if (_suppressSelectionHandlers || value is null || IsLoading)
        {
            return;
        }

        _ = LoadCaptureSelectionAsync(value.CaptureId);
    }

    partial void OnSelectedSectionChanged(SnapshotSectionListItem? value)
    {
        if (_suppressSelectionHandlers
            || value is null
            || _viewer.Current.CaptureId is not Guid captureId
            || IsLoading)
        {
            return;
        }

        SelectedSectionId = value.SectionId;
        _ = LoadSelectedSectionAsync(captureId, value.SectionId, CancellationToken.None);
    }

    partial void OnShowTechnicalViewChanged(bool value)
        => RebuildVisibleSections(_viewer.Current.Sections);

    partial void OnErrorTextChanged(string? value)
        => OnPropertyChanged(nameof(HasError));

    partial void OnSelectedConfigurationRecordChanged(SnapshotRecordListItem? value)
    {
        if (value is not null && SelectedObservationRecord is not null)
        {
            SelectedObservationRecord = null;
        }
    }

    partial void OnSelectedObservationRecordChanged(SnapshotRecordListItem? value)
    {
        if (value is not null && SelectedConfigurationRecord is not null)
        {
            SelectedConfigurationRecord = null;
        }
    }

    private async Task LoadCaptureSelectionAsync(Guid captureId)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        CancellationToken token = _loadCts.Token;
        IsLoading = true;
        try
        {
            SnapshotViewerLoadResult result = await Task.Run(
                    async () => await _viewer.LoadCaptureAsync(captureId, token).ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            ApplyResult(result);
            SnapshotSectionListItem? preferred = SnapshotPresentationIdentity
                .PreferOperatorFacingSection(VisibleSections.ToArray());
            if (result.Succeeded && preferred is not null)
            {
                SelectedSection = preferred;
                await LoadSelectedSectionAsync(captureId, preferred.SectionId, token)
                    .ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            ErrorText = "Load cancelled.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyResult(SnapshotViewerLoadResult result)
    {
        _suppressSelectionHandlers = true;
        try
        {
            Captures.Clear();
            foreach (SnapshotCaptureListItem capture in result.Captures)
            {
                Captures.Add(capture);
            }

            StatusText = result.StatusText;
            SchemaVersionText = result.SchemaVersion == 0
                ? "—"
                : result.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ConfigurationHashText = result.ConfigurationHashHex;
            ObservationHashText = result.ObservationHashHex;
            CapabilityHashText = result.CapabilityHashHex;
            CompletedAtText = result.CompletedAtText;
            ErrorText = result.Error;
            HintText = result.CaptureId is null
                ? (result.Error ?? HintText)
                : $"Capture {result.CaptureId:D}";
            RebuildVisibleSections(result.Sections);
            ApplySectionRecords(result);
            if (result.CaptureId is Guid captureId)
            {
                SelectedCapture = Captures.FirstOrDefault(c => c.CaptureId == captureId);
            }

            OnPropertyChanged(nameof(HasCapture));
            CopySanitizedCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            _suppressSelectionHandlers = false;
        }
    }

    private void ApplySectionRecords(SnapshotViewerLoadResult result)
    {
        SelectedConfigurationRecord = null;
        SelectedObservationRecord = null;
        ConfigurationRecords.Clear();
        foreach (SnapshotRecordListItem record in result.ConfigurationRecords)
        {
            ConfigurationRecords.Add(record);
        }

        ObservationRecords.Clear();
        foreach (SnapshotRecordListItem record in result.ObservationRecords)
        {
            ObservationRecords.Add(record);
        }
    }

    private void RebuildVisibleSections(IReadOnlyList<SnapshotSectionListItem> sections)
    {
        bool restore = _suppressSelectionHandlers;
        _suppressSelectionHandlers = true;
        try
        {
            string? previous = SelectedSection?.SectionId;
            VisibleSections.Clear();
            List<SnapshotSectionListItem> visible = [];
            foreach (SnapshotSectionListItem section in sections)
            {
                if (section.IsTechnicalOnly && !ShowTechnicalView)
                {
                    continue;
                }

                visible.Add(section);
            }

            foreach (SnapshotSectionListItem section in SnapshotPresentationIdentity.OrderOperatorFacing(visible))
            {
                VisibleSections.Add(section);
            }

            if (previous is not null)
            {
                SelectedSection = VisibleSections.FirstOrDefault(s => s.SectionId == previous);
            }
        }
        finally
        {
            _suppressSelectionHandlers = restore;
        }
    }

    private SnapshotViewerLoadResult BuildExportState()
        => new()
        {
            Succeeded = true,
            DeviceId = _viewer.Current.DeviceId,
            CaptureId = _viewer.Current.CaptureId,
            StatusText = StatusText,
            SchemaVersion = _viewer.Current.SchemaVersion,
            ConfigurationHashHex = ConfigurationHashText,
            ObservationHashHex = ObservationHashText,
            CapabilityHashHex = CapabilityHashText,
            SnapshotHashHex = _viewer.Current.SnapshotHashHex,
            CompletedAtText = CompletedAtText,
            Captures = Captures.ToArray(),
            Sections = _viewer.Current.Sections,
            ConfigurationRecords = ConfigurationRecords.ToArray(),
            ObservationRecords = ObservationRecords.ToArray(),
        };

    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.Clipboard;
        }

        return null;
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
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _captureCts?.Cancel();
        _captureCts?.Dispose();
    }

    private sealed record CaptureRunOutcome(CaptureProgress? LastProgress, IReadOnlyList<string> ProgressLines);
}
