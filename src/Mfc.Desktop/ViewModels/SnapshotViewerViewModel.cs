using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Read-only snapshot viewer: sections, config vs observations, hashes, schema version.
/// Network work stays off the UI thread; unknown properties require technical view.
/// </summary>
public sealed partial class SnapshotViewerViewModel : ObservableObject, IDisposable
{
    private readonly ISnapshotViewerService _viewer;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private CancellationTokenSource? _loadCts;
    private bool _disposed;
    private bool _suppressSelectionHandlers;

    public SnapshotViewerViewModel(
        ISnapshotViewerService viewer,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
        _connection.StateChanged += OnConnectionStateChanged;
    }

    public ObservableCollection<SnapshotCaptureListItem> Captures { get; } = [];

    public ObservableCollection<SnapshotSectionListItem> VisibleSections { get; } = [];

    public ObservableCollection<SnapshotRecordListItem> ConfigurationRecords { get; } = [];

    public ObservableCollection<SnapshotRecordListItem> ObservationRecords { get; } = [];

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showTechnicalView;

    [ObservableProperty]
    private string _statusText = "—";

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
    private string _hintText = "Select a device in inventory to view its latest completed snapshot.";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasCapture => !string.IsNullOrWhiteSpace(SelectedSectionId) || Captures.Count > 0;

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
           && _connection.State == ControllerConnectionState.Connected
           && _inventory.SelectedNode?.Kind == InventoryTreeKind.Device;

    private bool CanCopy()
        => !IsLoading && Captures.Count > 0;

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
            ReloadCommand.NotifyCanExecuteChanged();
        }
        else
        {
            Dispatcher.UIThread.Post(() => ReloadCommand.NotifyCanExecuteChanged());
        }
    }

    private void HandleSelectionChanged()
    {
        ReloadCommand.NotifyCanExecuteChanged();
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        if (selected is null || selected.Kind != InventoryTreeKind.Device)
        {
            _loadCts?.Cancel();
            _viewer.Clear();
            ApplyResult(new SnapshotViewerLoadResult { Succeeded = false });
            HintText = "Select a device in inventory to view its latest completed snapshot.";
            return;
        }

        if (ReloadCommand.CanExecute(null))
        {
            ReloadCommand.Execute(null);
        }
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
        ReloadCommand.NotifyCanExecuteChanged();
        CopySanitizedCommand.NotifyCanExecuteChanged();
        try
        {
            SnapshotViewerLoadResult result = await Task.Run(
                    async () => await _viewer.LoadDeviceAsync(deviceId, token).ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            ApplyResult(result);
            if (result is { Succeeded: true, CaptureId: Guid captureId, Sections.Count: > 0 })
            {
                SnapshotSectionListItem? firstVisible = VisibleSections.FirstOrDefault();
                if (firstVisible is not null)
                {
                    SelectedSection = firstVisible;
                    await LoadSelectedSectionAsync(captureId, firstVisible.SectionId, token)
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
            ReloadCommand.NotifyCanExecuteChanged();
            CopySanitizedCommand.NotifyCanExecuteChanged();
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
            CopySanitizedCommand.NotifyCanExecuteChanged();
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
            SnapshotSectionListItem? firstVisible = VisibleSections.FirstOrDefault();
            if (result.Succeeded && firstVisible is not null)
            {
                SelectedSection = firstVisible;
                await LoadSelectedSectionAsync(captureId, firstVisible.SectionId, token)
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
            ReloadCommand.NotifyCanExecuteChanged();
            CopySanitizedCommand.NotifyCanExecuteChanged();
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
            foreach (SnapshotSectionListItem section in sections)
            {
                if (section.IsTechnicalOnly && !ShowTechnicalView)
                {
                    continue;
                }

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
    }
}
