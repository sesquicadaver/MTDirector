using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>
/// Node module: topology, zones summary, onboarding readiness, workflow, device hashes.
/// Composes Contracts-backed Inventory / Zones / Onboarding presentation — no Domain/SQL.
/// </summary>
public sealed partial class NodeDetailViewModel : ObservableObject, IDisposable
{
    private readonly InventoryTreeViewModel _inventory;
    private readonly ZonesViewModel _zones;
    private readonly OnboardingViewModel _onboarding;
    private bool _disposed;

    public NodeDetailViewModel(
        InventoryTreeViewModel inventory,
        ZonesViewModel zones,
        OnboardingViewModel onboarding)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _zones = zones ?? throw new ArgumentNullException(nameof(zones));
        _onboarding = onboarding ?? throw new ArgumentNullException(nameof(onboarding));
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
        _zones.PropertyChanged += OnZonesPropertyChanged;
        _onboarding.PropertyChanged += OnOnboardingPropertyChanged;
        RefreshPresentation();
    }

    public ObservableCollection<string> DeviceHashLines { get; } = [];

    public ObservableCollection<string> ZoneSummaryLines { get; } = [];

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

    [RelayCommand]
    private void Refresh() => RefreshPresentation();

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

    private void PostRefresh()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshPresentation();
        }
        else
        {
            Dispatcher.UIThread.Post(RefreshPresentation);
        }
    }

    private void RefreshPresentation()
    {
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        InventoryNodeViewModel? node = ResolveNode(selected);
        DeviceHashLines.Clear();
        ZoneSummaryLines.Clear();

        if (node is null)
        {
            SelectionHint = "Select a Node (or Device under a Node) in the inventory tree.";
            TopologyText = "Topology: —";
            WorkflowStatusText = "—";
            OnboardingReadinessText = _onboarding.StatusText;
            DeploymentReadinessText = "Select a Node to assess readiness.";
            return;
        }

        SelectionHint = $"Node: {node.DisplayName}";
        TopologyText =
            $"Kind: {OrDash(node.NodeKindText)}; Uplink: {OrDash(node.UplinkModeText)}; Status: {OrDash(node.StatusText)}";
        WorkflowStatusText = OrDash(node.WorkflowStatusText);
        OnboardingReadinessText = string.IsNullOrWhiteSpace(_onboarding.StatusText)
            ? "—"
            : _onboarding.StatusText;
        DeploymentReadinessText =
            $"Workflow={WorkflowStatusText}; Zones hint={_zones.SelectedNodeHint}; " +
            $"Onboarding={OnboardingReadinessText}";

        foreach (InventoryNodeViewModel device in node.Children.Where(static c => c.Kind == InventoryTreeKind.Device))
        {
            DeviceHashLines.Add(
                $"{device.DisplayName}: desired={OrDash(device.DesiredHashText)} " +
                $"committed={OrDash(device.CommittedHashText)} actual={OrDash(device.ActualHashText)} " +
                $"({OrDash(device.SupportStateText)} / {OrDash(device.ReachabilityText)})");
        }

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

    private static string OrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _inventory.PropertyChanged -= OnInventoryPropertyChanged;
        _zones.PropertyChanged -= OnZonesPropertyChanged;
        _onboarding.PropertyChanged -= OnOnboardingPropertyChanged;
    }
}
