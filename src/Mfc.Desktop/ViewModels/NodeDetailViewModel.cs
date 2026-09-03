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
/// Node module: topology, zones summary, onboarding readiness, workflow, device hashes,
/// VRRP pair consistency (W6-02).
/// Composes Contracts-backed Inventory / Zones / Onboarding / Snapshot presentation — no Domain/SQL.
/// Canonical workflow comes from GetNodeWorkflow, not an ad-hoc Zones+Onboarding mashup.
/// </summary>
public sealed partial class NodeDetailViewModel : ObservableObject, IDisposable
{
    private readonly InventoryTreeViewModel _inventory;
    private readonly ZonesViewModel _zones;
    private readonly OnboardingViewModel _onboarding;
    private readonly IInventoryTreeClient _inventoryClient;
    private readonly ISnapshotViewerClient _snapshotClient;
    private readonly IControllerConnectionService _connection;
    private readonly object _workflowApplyGate = new();
    private int _workflowEpoch;
    private bool _disposed;

    public NodeDetailViewModel(
        InventoryTreeViewModel inventory,
        ZonesViewModel zones,
        OnboardingViewModel onboarding,
        IInventoryTreeClient inventoryClient,
        ISnapshotViewerClient snapshotClient,
        IControllerConnectionService connection)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _zones = zones ?? throw new ArgumentNullException(nameof(zones));
        _onboarding = onboarding ?? throw new ArgumentNullException(nameof(onboarding));
        _inventoryClient = inventoryClient ?? throw new ArgumentNullException(nameof(inventoryClient));
        _snapshotClient = snapshotClient ?? throw new ArgumentNullException(nameof(snapshotClient));
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

    /// <summary>VRRP pair members (a/b) when <see cref="IsVrrpNode"/>; roles only from backend labels.</summary>
    public ObservableCollection<VrrpMemberListItem> VrrpMembers { get; } = [];

    public ObservableCollection<string> ZoneSummaryLines { get; } = [];

    /// <summary>Per-device contributing/sync lines from GetNodeWorkflow.</summary>
    public ObservableCollection<string> WorkflowDeviceLines { get; } = [];

    /// <summary>W6-02 pair consistency findings for the selected VRRP Node.</summary>
    public ObservableCollection<VrrpPairFindingListItem> VrrpPairFindings { get; } = [];

    public bool HasDeviceMembers => DeviceMembers.Count > 0;

    public bool HasNoDeviceMembers => DeviceMembers.Count == 0;

    public bool HasVrrpMembers => VrrpMembers.Count > 0;

    public bool HasNoVrrpMembers => IsVrrpNode && VrrpMembers.Count == 0;

    public bool HasStandaloneDeviceList => !IsVrrpNode && HasDeviceMembers;

    public bool HasStandaloneDeviceEmpty => !IsVrrpNode && HasNoDeviceMembers;

    public bool ShowStandaloneDeviceSection => !IsVrrpNode;

    public bool HasSelectedVrrpMember => SelectedVrrpMember is not null;

    public bool HasWorkflowDeviceLines => WorkflowDeviceLines.Count > 0;

    public bool HasNoWorkflowDeviceLines => WorkflowDeviceLines.Count == 0;

    public bool HasVrrpPairFindings => VrrpPairFindings.Count > 0;

    public bool HasNoVrrpPairFindings => IsVrrpNode && VrrpPairFindings.Count == 0;

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
    [NotifyPropertyChangedFor(nameof(HasNoVrrpMembers))]
    [NotifyPropertyChangedFor(nameof(HasStandaloneDeviceList))]
    [NotifyPropertyChangedFor(nameof(HasStandaloneDeviceEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowStandaloneDeviceSection))]
    [NotifyPropertyChangedFor(nameof(HasNoVrrpPairFindings))]
    [NotifyCanExecuteChangedFor(nameof(ValidateVrrpPairCommand))]
    [NotifyCanExecuteChangedFor(nameof(CaptureAllMembersAndValidateCommand))]
    private bool _isVrrpNode;

    [ObservableProperty]
    private string _vrrpPairHint = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedVrrpMember))]
    private VrrpMemberListItem? _selectedVrrpMember;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private string _vrrpPairStatusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(ValidateVrrpPairCommand))]
    [NotifyCanExecuteChangedFor(nameof(CaptureAllMembersAndValidateCommand))]
    private bool _isBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        RefreshPresentation();
        await LoadNodeWorkflowAsync().ConfigureAwait(true);
        if (IsVrrpNode)
        {
            await ValidateVrrpPairInternalAsync(liveCapture: false).ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanValidateVrrpPair))]
    private async Task ValidateVrrpPairAsync()
        => await ValidateVrrpPairInternalAsync(liveCapture: false).ConfigureAwait(true);

    [RelayCommand(CanExecute = nameof(CanValidateVrrpPair))]
    private async Task CaptureAllMembersAndValidateAsync()
        => await ValidateVrrpPairInternalAsync(liveCapture: true).ConfigureAwait(true);

    private bool CanRefresh() => !IsBusy;

    private bool CanValidateVrrpPair()
        => !IsBusy
           && IsVrrpNode
           && _connection.State == ControllerConnectionState.Connected
           && ResolveNode(_inventory.SelectedNode) is not null;

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
        lock (_workflowApplyGate)
        {
            DeviceHashLines.Clear();
            DeviceMembers.Clear();
            VrrpMembers.Clear();
            SelectedVrrpMember = null;
            ZoneSummaryLines.Clear();
            WorkflowDeviceLines.Clear();
            VrrpPairFindings.Clear();
            VrrpPairStatusText = string.Empty;
            NotifyWorkflowDeviceLinesChanged();
            NotifyVrrpPairFindingsChanged();
            ErrorText = null;

            if (node is null)
            {
                SelectionHint = "Select a Node (or Device under a Node) in the inventory tree.";
                TopologyText = "Topology: —";
                WorkflowStatusText = "—";
                OnboardingReadinessText = _onboarding.StatusText;
                DeploymentReadinessText = "Select a Node to load deployment readiness.";
                IsVrrpNode = false;
                VrrpPairHint = string.Empty;
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
            ? "Loading deployment readiness…"
            : "Connect to Controller to load deployment readiness.";

            IsVrrpNode = string.Equals(node.NodeKindText, "Vrrp", StringComparison.Ordinal);
            VrrpPairHint = IsVrrpNode
                ? "VRRP pair consistency checks VIP/config agreement and logical firewall from last captures (or Capture all members). Blockers gate Onboarding Validate and Deploy CreatePlan."
                : string.Empty;
            ValidateVrrpPairCommand.NotifyCanExecuteChanged();
            CaptureAllMembersAndValidateCommand.NotifyCanExecuteChanged();

            int slot = 0;
            foreach (InventoryNodeViewModel device in node.Children.Where(static c => c.Kind == InventoryTreeKind.Device))
            {
                DeviceMembers.Add(device);
                DeviceHashLines.Add(
                    $"{device.DisplayName}: desired policy digest={OrDash(device.DesiredHashText)} " +
                    $"committed policy digest={OrDash(device.CommittedHashText)} actual managed digest={OrDash(device.ActualHashText)} " +
                    $"({OrDash(device.SupportStateText)} / {OrDash(device.ReachabilityText)})");
                if (IsVrrpNode)
                {
                    VrrpMembers.Add(new VrrpMemberListItem
                    {
                        SlotText = ((char)('a' + slot)).ToString(),
                        DeviceId = device.Id,
                        DisplayName = device.DisplayName,
                        RoleText = device.HasVrrpRoles ? device.VrrpRolesText : "—",
                        HasRole = device.HasVrrpRoles,
                        ManagementHostText = OrDash(device.ManagementHostText),
                        LastSnapshotText = OrDash(device.LastSnapshotText),
                        ReachabilityText = OrDash(device.ReachabilityText),
                    });
                    slot++;
                }
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
            DeploymentReadinessText = "Connect to Controller to load deployment readiness.";
            return;
        }

        IsBusy = true;
        Guid nodeId = node.Id;
        try
        {
            NodeWorkflow workflow = await Task.Run(
                    async () => await _inventoryClient.GetNodeWorkflowAsync(nodeId).ConfigureAwait(false))
                .ConfigureAwait(true);
            lock (_workflowApplyGate)
            {
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

    private async Task ValidateVrrpPairInternalAsync(bool liveCapture)
    {
        InventoryNodeViewModel? node = ResolveNode(_inventory.SelectedNode);
        if (node is null || !IsVrrpNode)
        {
            return;
        }

        if (_connection.State != ControllerConnectionState.Connected)
        {
            VrrpPairStatusText = "Connect to Controller to validate VRRP pair consistency.";
            return;
        }

        IsBusy = true;
        ErrorText = null;
        try
        {
            if (liveCapture)
            {
                VrrpPairStatusText = "Capturing all VRRP members (StartCapture node_id)…";
                StartCaptureResponse started = await _snapshotClient
                    .StartNodeCaptureAsync(node.Id, Guid.NewGuid(), CancellationToken.None)
                    .ConfigureAwait(true);
                CaptureProgress? last = null;
                await foreach (CaptureProgress progress in _snapshotClient
                                   .WatchCaptureAsync(
                                       DesktopProtoUuid.ToGuid(started.OperationId),
                                       CancellationToken.None)
                                   .ConfigureAwait(true))
                {
                    last = progress;
                    Guid progressDevice = DesktopProtoUuid.ToGuid(progress.DeviceId);
                    string memberName = VrrpMembers.FirstOrDefault(m => m.DeviceId == progressDevice)?.DisplayName
                        ?? (progressDevice == Guid.Empty ? "node" : progressDevice.ToString("D"));
                    VrrpPairStatusText = $"{memberName}: {progress.Stage}";
                }

                if (last is null || last.Stage != CaptureStage.Completed)
                {
                    throw new InvalidOperationException(
                        "Node capture did not complete successfully for all VRRP members.");
                }
            }

            VrrpPairStatusText = "Validating VRRP pair consistency from last captures…";
            VrrpPairConsistencyReport report = await _inventoryClient
                .ValidateVrrpPairConsistencyAsync(node.Id, CancellationToken.None)
                .ConfigureAwait(true);
            VrrpPairFindings.Clear();
            foreach (VrrpPairConsistencyFinding finding in report.Findings)
            {
                VrrpPairFindings.Add(new VrrpPairFindingListItem
                {
                    Code = finding.Code,
                    Severity = finding.Severity,
                    Message = finding.Message,
                    Subject = finding.HasSubject ? finding.Subject : string.Empty,
                });
            }

            NotifyVrrpPairFindingsChanged();
            VrrpPairStatusText = report.Passed
                ? $"Pair consistency passed (members={report.MemberCount}, captures={report.CaptureCount})."
                : $"Pair consistency has blockers (members={report.MemberCount}, captures={report.CaptureCount}).";
        }
        catch (RpcException ex)
        {
            ErrorText = ex.Status.Detail;
            VrrpPairStatusText = "VRRP pair consistency failed.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            VrrpPairStatusText = "VRRP pair consistency failed.";
        }
        finally
        {
            IsBusy = false;
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
        OnPropertyChanged(nameof(HasVrrpMembers));
        OnPropertyChanged(nameof(HasNoVrrpMembers));
        OnPropertyChanged(nameof(HasStandaloneDeviceList));
        OnPropertyChanged(nameof(HasStandaloneDeviceEmpty));
        OnPropertyChanged(nameof(ShowStandaloneDeviceSection));
    }

    private void NotifyWorkflowDeviceLinesChanged()
    {
        OnPropertyChanged(nameof(HasWorkflowDeviceLines));
        OnPropertyChanged(nameof(HasNoWorkflowDeviceLines));
    }

    private void NotifyVrrpPairFindingsChanged()
    {
        OnPropertyChanged(nameof(HasVrrpPairFindings));
        OnPropertyChanged(nameof(HasNoVrrpPairFindings));
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

/// <summary>One VRRP pair member for the Node-centric table (Contracts-only labels).</summary>
public sealed class VrrpMemberListItem
{
    public required string SlotText { get; init; }

    public required Guid DeviceId { get; init; }

    public required string DisplayName { get; init; }

    public required string RoleText { get; init; }

    public required bool HasRole { get; init; }

    public required string ManagementHostText { get; init; }

    public required string LastSnapshotText { get; init; }

    public required string ReachabilityText { get; init; }

    public string SummaryLine =>
        $"{SlotText}: {DisplayName} · role={RoleText} · mgmt={ManagementHostText} · last={LastSnapshotText}";
}

/// <summary>One VRRP pair consistency finding (Contracts-only; W6-02).</summary>
public sealed class VrrpPairFindingListItem
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public required string Subject { get; init; }

    public string SummaryLine =>
        string.IsNullOrWhiteSpace(Subject)
            ? $"[{Severity}] {Code}: {Message}"
            : $"[{Severity}] {Code} ({Subject}): {Message}";
}
