using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>Zones + node bindings CRUD panel with resolve blockers (Contracts-only).</summary>
public sealed partial class ZonesViewModel : ObservableObject, IDisposable
{
    private readonly IZonePanelService _zones;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private bool _disposed;

    public ZonesViewModel(
        IZonePanelService zones,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _zones = zones ?? throw new ArgumentNullException(nameof(zones));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _connection.StateChanged += OnConnectionStateChanged;
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
        BindingKinds =
        [
            NodeZoneBindingKind.InterfaceList,
            NodeZoneBindingKind.SingleInterface,
            NodeZoneBindingKind.ExplicitInterfaceSet,
        ];
        SelectedBindingKind = NodeZoneBindingKind.SingleInterface;
    }

    public ObservableCollection<ZoneDefinitionListItem> Zones { get; } = [];

    public ObservableCollection<NodeZoneBindingListItem> Bindings { get; } = [];

    public ObservableCollection<ZoneResolveResultListItem> ResolveResults { get; } = [];

    public IReadOnlyList<NodeZoneBindingKind> BindingKinds { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public string SelectedNodeHint
    {
        get
        {
            InventoryNodeViewModel? selected = _inventory.SelectedNode;
            if (selected is null)
            {
                return "Select a Node in the inventory tree to manage bindings.";
            }

            return selected.KindLabel switch
            {
                "Node" => $"Node: {selected.DisplayName}",
                "Device" => $"Device selected — use parent Node for bindings ({selected.DisplayName}).",
                _ => "Select a Node (not Site) for bindings.",
            };
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ZoneDefinitionListItem? _selectedZone;

    [ObservableProperty]
    private NodeZoneBindingListItem? _selectedBinding;

    [ObservableProperty]
    private string _newZoneKey = string.Empty;

    [ObservableProperty]
    private string _newZoneName = string.Empty;

    [ObservableProperty]
    private string _newZoneDescription = string.Empty;

    [ObservableProperty]
    private NodeZoneBindingKind _selectedBindingKind;

    [ObservableProperty]
    private string _bindingValuesText = string.Empty;

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task RefreshAsync()
    {
        await RunBusyAsync(async ct =>
        {
            IReadOnlyList<ZoneDefinitionListItem> zones = await _zones.ListZonesAsync(ct).ConfigureAwait(true);
            Zones.Clear();
            foreach (ZoneDefinitionListItem zone in zones)
            {
                Zones.Add(zone);
            }

            Guid? nodeId = TryGetSelectedNodeId();
            Bindings.Clear();
            ResolveResults.Clear();
            if (nodeId is Guid id)
            {
                IReadOnlyList<NodeZoneBindingListItem> bindings = await _zones.ListBindingsAsync(id, ct)
                    .ConfigureAwait(true);
                foreach (NodeZoneBindingListItem binding in bindings)
                {
                    Bindings.Add(binding);
                }
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task CreateZoneAsync()
    {
        await RunBusyAsync(async ct =>
        {
            ZoneDefinitionListItem created = await _zones.CreateCompanyZoneAsync(
                    NewZoneKey.Trim(),
                    NewZoneName.Trim(),
                    string.IsNullOrWhiteSpace(NewZoneDescription) ? null : NewZoneDescription.Trim(),
                    ct)
                .ConfigureAwait(true);
            Zones.Add(created);
            SelectedZone = created;
            NewZoneKey = string.Empty;
            NewZoneName = string.Empty;
            NewZoneDescription = string.Empty;
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task DeleteZoneAsync()
    {
        if (SelectedZone is null)
        {
            ErrorText = "Select a zone to delete.";
            return;
        }

        ZoneDefinitionListItem zone = SelectedZone;
        await RunBusyAsync(async ct =>
        {
            await _zones.DeleteZoneAsync(zone, ct).ConfigureAwait(true);
            Zones.Remove(zone);
            if (ReferenceEquals(SelectedZone, zone))
            {
                SelectedZone = null;
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task UpsertBindingAsync()
    {
        Guid? nodeId = TryGetSelectedNodeId();
        if (nodeId is null)
        {
            ErrorText = "Select a Node in the inventory tree.";
            return;
        }

        if (SelectedZone is null)
        {
            ErrorText = "Select a zone for the binding.";
            return;
        }

        string[] values = BindingValuesText
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0)
        {
            ErrorText = "Enter at least one binding value.";
            return;
        }

        Guid zoneId = SelectedZone.Id;
        NodeZoneBindingListItem? existing = Bindings.FirstOrDefault(b => b.ZoneId == zoneId);
        await RunBusyAsync(async ct =>
        {
            NodeZoneBindingListItem saved = await _zones.UpsertBindingAsync(
                    nodeId.Value,
                    zoneId,
                    SelectedBindingKind,
                    values,
                    existing?.RowVersion,
                    ct)
                .ConfigureAwait(true);
            if (existing is not null)
            {
                int index = Bindings.IndexOf(existing);
                if (index >= 0)
                {
                    Bindings[index] = saved;
                }
            }
            else
            {
                Bindings.Add(saved);
            }

            SelectedBinding = saved;
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task DeleteBindingAsync()
    {
        if (SelectedBinding is null)
        {
            ErrorText = "Select a binding to delete.";
            return;
        }

        NodeZoneBindingListItem binding = SelectedBinding;
        await RunBusyAsync(async ct =>
        {
            await _zones.DeleteBindingAsync(binding, ct).ConfigureAwait(true);
            Bindings.Remove(binding);
            if (ReferenceEquals(SelectedBinding, binding))
            {
                SelectedBinding = null;
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ResolveAsync()
    {
        Guid? nodeId = TryGetSelectedNodeId();
        if (nodeId is null)
        {
            ErrorText = "Select a Node in the inventory tree.";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            IReadOnlyList<ZoneResolveResultListItem> results = await _zones
                .ResolveForNodeAsync(nodeId.Value, ct)
                .ConfigureAwait(true);
            ResolveResults.Clear();
            foreach (ZoneResolveResultListItem item in results)
            {
                ResolveResults.Add(item);
            }

            // Refresh bindings so AnalysisStale / RowVersion reflect RecordResolve.
            Bindings.Clear();
            foreach (NodeZoneBindingListItem binding in await _zones.ListBindingsAsync(nodeId.Value, ct)
                         .ConfigureAwait(true))
            {
                Bindings.Add(binding);
            }
        }).ConfigureAwait(true);
    }

    private bool CanOperate()
        => !IsBusy && _connection.State == ControllerConnectionState.Connected;

    private Guid? TryGetSelectedNodeId()
    {
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        if (selected is null)
        {
            return null;
        }

        if (string.Equals(selected.KindLabel, "Node", StringComparison.Ordinal))
        {
            return selected.Id;
        }

        if (string.Equals(selected.KindLabel, "Device", StringComparison.Ordinal)
            && selected.ParentId is Guid parent)
        {
            return parent;
        }

        return null;
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> action)
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller first.";
            return;
        }

        IsBusy = true;
        NotifyCommands();
        ErrorText = null;
        try
        {
            await action(CancellationToken.None).ConfigureAwait(true);
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
            NotifyCommands();
            OnPropertyChanged(nameof(SelectedNodeHint));
        }
    }

    private void NotifyCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        CreateZoneCommand.NotifyCanExecuteChanged();
        DeleteZoneCommand.NotifyCanExecuteChanged();
        UpsertBindingCommand.NotifyCanExecuteChanged();
        DeleteBindingCommand.NotifyCanExecuteChanged();
        ResolveCommand.NotifyCanExecuteChanged();
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

    private void OnInventoryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InventoryTreeViewModel.SelectedNode))
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                OnPropertyChanged(nameof(SelectedNodeHint));
            }
            else
            {
                Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(SelectedNodeHint)));
            }
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
        _inventory.PropertyChanged -= OnInventoryPropertyChanged;
    }
}
