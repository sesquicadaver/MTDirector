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
/// Node module: topology, zones summary, onboarding readiness, workflow, device hashes.
/// Composes Contracts-backed Inventory / Zones / Onboarding presentation — no Domain/SQL.
/// Canonical workflow comes from GetNodeWorkflow, not an ad-hoc Zones+Onboarding mashup.
/// </summary>
public sealed partial class NodeDetailViewModel : ObservableObject, IDisposable
{
    private readonly InventoryTreeViewModel _inventory;
    private readonly ZonesViewModel _zones;
    private readonly OnboardingViewModel _onboarding;
    private readonly IInventoryTreeClient _inventoryClient;
    private readonly IControllerConnectionService _connection;
    private int _workflowEpoch;
    private bool _disposed;

    public NodeDetailViewModel(
        InventoryTreeViewModel inventory,
        ZonesViewModel zones,
        OnboardingViewModel onboarding,
        IInventoryTreeClient inventoryClient,
        IControllerConnectionService connection)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _zones = zones ?? throw new ArgumentNullException(nameof(zones));
        _onboarding = onboarding ?? throw new ArgumentNullException(nameof(onboarding));
        _inventoryClient = inventoryClient ?? throw new ArgumentNullException(nameof(inventoryClient));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
        _zones.PropertyChanged += OnZonesPropertyChanged;
        _onboarding.PropertyChanged += OnOnboardingPropertyChanged;
        _connection.StateChanged += OnConnectionStateChanged;
        RefreshPresentation();
        _ = LoadNodeWorkflowAsync();
    }

    public ObservableCollection<string> DeviceHashLines { get; } = [];

    /// <summary>Device children of the resolved Node — explicit fields, not DetailSummary.</summary>
    public ObservableCollection<InventoryNodeViewModel> DeviceMembers { get; } = [];

    public ObservableCollection<string> ZoneSummaryLines { get; } = [];

    /// <summary>Per-device contributing/sync lines from GetNodeWorkflow.</summary>
    public ObservableCollection<string> WorkflowDeviceLines { get; } = [];

    public bool HasDeviceMembers => DeviceMembers.Count > 0;

    public bool HasNoDeviceMembers => DeviceMembers.Count == 0;

    public bool HasWorkflowDeviceLines => WorkflowDeviceLines.Count > 0;

    public bool HasNoWorkflowDeviceLines => WorkflowDeviceLines.Count == 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    [ObservableProperty]
    private string _topologyText = "Select a Node in the inventory tree.";

    [ObservableProperty]
    private string _workflowStatusText = "—";

    [ObservableProperty]
    private string _onboardingReadinessText = "—";

    [ObservableProperty]
    private string _deploymentReadinessText = "—";

    [ObservableProperty]
    private string _selectionHint = "No Node selected.";

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        RefreshPresentation();
        await LoadNodeWorkflowAsync().ConfigureAwait(true);
    }

    private bool CanRefresh() => !IsBusy;

    private void OnInventoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InventoryTreeViewModel.SelectedNode)
            or nameof(InventoryTreeViewModel.Roots))
        {
            PostRefresh();
        }
    }

    private void OnZonesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ZonesViewModel.SelectedNodeHint)
            or nameof(ZonesViewModel.IsBusy)
            or nameof(ZonesViewModel.ErrorText))
        {
            PostRefresh();
        }
    }

    private void OnOnboardingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OnboardingViewModel.StatusText)
            or nameof(OnboardingViewModel.RecoveryFactsText))
        {
            PostRefresh();
        }
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e) => PostRefresh();

    private void PostRefresh()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            _ = RefreshAsync();
        }
        else
        {
            Dispatcher.UIThread.Post(() => _ = RefreshAsync());
        }
    }

    private void RefreshPresentation()
    {
        Interlocked.Increment(ref _workflowEpoch);
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        InventoryNodeViewModel? node = ResolveNode(selected);
        DeviceHashLines.Clear();
        DeviceMembers.Clear();
        ZoneSummaryLines.Clear();
        WorkflowDeviceLines.Clear();
        NotifyWorkflowDeviceLinesChanged();
        ErrorText = null;

        if (node is null)
        {
            SelectionHint = "Select a Node (or Device under a Node) in the inventory tree.";
            TopologyText = "Topology: —";
            WorkflowStatusText = "—";
            OnboardingReadinessText = _onboarding.StatusText;
            DeploymentReadinessText = "Select a Node to load GetNodeWorkflow.";
            NotifyDeviceMembersChanged();
            return;
        }

        SelectionHint = $"Node: {node.DisplayName}";
        TopologyText =
            $"Kind: {OrDash(node.NodeKindText)}; Uplink: {OrDash(node.UplinkModeText)}; Status: {OrDash(node.StatusText)}";
        WorkflowStatusText = OrDash(node.WorkflowStatusText);
        OnboardingReadinessText = string.IsNullOrWhiteSpace(_onboarding.StatusText)
            ? "—"
            : _onboarding.StatusText;
        DeploymentReadinessText = _connection.State == ControllerConnectionState.Connected
            ? "Loading GetNodeWorkflow…"
            : "Connect to Controller to load GetNodeWorkflow.";

        foreach (InventoryNodeViewModel device in node.Children.Where(static c => c.Kind == InventoryTreeKind.Device))
        {
            DeviceMembers.Add(device);
            DeviceHashLines.Add(
                $"{device.DisplayName}: desired={OrDash(device.DesiredHashText)} " +
                $"committed={OrDash(device.CommittedHashText)} actual={OrDash(device.ActualHashText)} " +
                $"({OrDash(device.SupportStateText)} / {OrDash(device.ReachabilityText)})");
        }

        NotifyDeviceMembersChanged();

        ZoneSummaryLines.Add(_zones.SelectedNodeHint);
        foreach (NodeZoneBindingListItem binding in _zones.Bindings.Take(32))
        {
            ZoneSummaryLines.Add(binding.SummaryLine);
        }

        if (_zones.Bindings.Count == 0)
        {
            ZoneSummaryLines.Add(
                "No zone bindings loaded for the selected Node (refresh Zones while a Node is selected).");
        }
    }

    private async Task LoadNodeWorkflowAsync()
    {
        int epoch = Interlocked.Increment(ref _workflowEpoch);
        InventoryNodeViewModel? node = ResolveNode(_inventory.SelectedNode);
        if (node is null)
        {
            return;
        }

        if (_connection.State != ControllerConnectionState.Connected)
        {
            DeploymentReadinessText = "Connect to Controller to load GetNodeWorkflow.";
            return;
        }

        IsBusy = true;
        Guid nodeId = node.Id;
        try
        {
            NodeWorkflow workflow = await Task.Run(
                    async () => await _inventoryClient.GetNodeWorkflowAsync(nodeId).ConfigureAwait(false))
                .ConfigureAwait(true);
            if (epoch != Volatile.Read(ref _workflowEpoch))
            {
                return;
            }

            WorkflowStatusText = FormatEnum(workflow.WorkflowStatus);
            DeploymentReadinessText = WorkflowStatusText;
            WorkflowDeviceLines.Clear();
            foreach (DeviceWorkflowProjection device in workflow.Devices)
            {
                Guid deviceId = DesktopProtoUuid.ToGuid(device.DeviceId);
                string name = DeviceMembers.FirstOrDefault(member => member.Id == deviceId)?.DisplayName
                    ?? deviceId.ToString("D");
                WorkflowDeviceLines.Add(
                    $"{name}: contributing={FormatEnum(device.ContributingStatus)}; " +
                    $"sync={FormatEnum(device.SyncClassification)}");
            }

            NotifyWorkflowDeviceLinesChanged();
            ErrorText = null;
        }
        catch (OperationCanceledException)
        {
            if (epoch != Volatile.Read(ref _workflowEpoch))
            {
                return;
            }

            DeploymentReadinessText = "GetNodeWorkflow cancelled.";
        }
        catch (RpcException ex)
        {
            if (epoch != Volatile.Read(ref _workflowEpoch))
            {
                return;
            }

            ErrorText = ex.Status.Detail;
            DeploymentReadinessText = "GetNodeWorkflow failed.";
        }
        catch (Exception ex)
        {
            if (epoch != Volatile.Read(ref _workflowEpoch))
            {
                return;
            }

            ErrorText = ex.Message;
            DeploymentReadinessText = "GetNodeWorkflow failed.";
        }
        finally
        {
            if (epoch == Volatile.Read(ref _workflowEpoch))
            {
                IsBusy = false;
            }
        }
    }

    private InventoryNodeViewModel? ResolveNode(InventoryNodeViewModel? selected)
    {
        if (selected is null)
        {
            return null;
        }

        if (selected.Kind == InventoryTreeKind.Node)
        {
            return selected;
        }

        if (selected.Kind == InventoryTreeKind.Device && selected.ParentId is Guid parentId)
        {
            foreach (InventoryNodeViewModel site in _inventory.Roots)
            {
                foreach (InventoryNodeViewModel candidate in site.Children)
                {
                    if (candidate.Kind == InventoryTreeKind.Node && candidate.Id == parentId)
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    private void NotifyDeviceMembersChanged()
    {
        OnPropertyChanged(nameof(HasDeviceMembers));
        OnPropertyChanged(nameof(HasNoDeviceMembers));
    }

    private void NotifyWorkflowDeviceLinesChanged()
    {
        OnPropertyChanged(nameof(HasWorkflowDeviceLines));
        OnPropertyChanged(nameof(HasNoWorkflowDeviceLines));
    }

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private static string OrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string FormatEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string raw = value.ToString();
        if (string.IsNullOrWhiteSpace(raw) || raw.EndsWith("Unspecified", StringComparison.Ordinal))
        {
            return "—";
        }

        return raw;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Increment(ref _workflowEpoch);
        _inventory.PropertyChanged -= OnInventoryPropertyChanged;
        _zones.PropertyChanged -= OnZonesPropertyChanged;
        _onboarding.PropertyChanged -= OnOnboardingPropertyChanged;
        _connection.StateChanged -= OnConnectionStateChanged;
    }
}
