using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>Inventory tree presentation: refresh, cached badge, error text. No Domain/SQL/RouterOS logic.</summary>
public sealed partial class InventoryTreeViewModel : ObservableObject, IDisposable
{
    private readonly IInventoryTreeService _treeService;
    private readonly IControllerConnectionService _connection;
    private CancellationTokenSource? _refreshCts;
    private bool _disposed;

    public InventoryTreeViewModel(IInventoryTreeService treeService, IControllerConnectionService connection)
    {
        _treeService = treeService ?? throw new ArgumentNullException(nameof(treeService));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.StateChanged += OnConnectionStateChanged;
    }

    public ObservableCollection<InventoryNodeViewModel> Roots { get; } = [];

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private bool _isCached;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private InventoryNodeViewModel? _selectedNode;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasSelection => SelectedNode is not null;

    public string CachedBadgeText => IsCached ? "Cached" : string.Empty;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller before refreshing inventory.";
            return;
        }

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        CancellationToken token = _refreshCts.Token;

        IsRefreshing = true;
        RefreshCommand.NotifyCanExecuteChanged();
        try
        {
            InventoryTreeLoadResult result = await Task.Run(
                    async () => await _treeService.RefreshAsync(token).ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);
            ApplyResult(result);
        }
        catch (OperationCanceledException)
        {
            InventoryTreeLoadResult current = _treeService.Current;
            ApplyResult(new InventoryTreeLoadResult
            {
                Roots = current.Roots,
                Succeeded = current.Succeeded,
                Error = "Refresh cancelled.",
                IsCached = current.Roots.Count > 0 || current.IsCached,
                IsRefreshing = false,
            });
        }
        finally
        {
            IsRefreshing = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRefresh()
        => !IsRefreshing && _connection.State == ControllerConnectionState.Connected;

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            HandleConnectionStateChanged();
        }
        else
        {
            Dispatcher.UIThread.Post(HandleConnectionStateChanged);
        }
    }

    private void HandleConnectionStateChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        if (_connection.State == ControllerConnectionState.Connected)
        {
            if (RefreshCommand.CanExecute(null))
            {
                RefreshCommand.Execute(null);
            }
        }
    }

    private void ApplyResult(InventoryTreeLoadResult result)
    {
        Roots.Clear();
        foreach (InventoryTreeItem root in result.Roots)
        {
            Roots.Add(new InventoryNodeViewModel(root));
        }

        ErrorText = result.Error;
        IsCached = result.IsCached;
        IsRefreshing = result.IsRefreshing;
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CachedBadgeText));
        if (SelectedNode is not null
            && Roots.SelectMany(Flatten).All(n => n.Id != SelectedNode.Id))
        {
            SelectedNode = null;
        }
    }

    private static IEnumerable<InventoryNodeViewModel> Flatten(InventoryNodeViewModel node)
    {
        yield return node;
        foreach (InventoryNodeViewModel child in node.Children.SelectMany(Flatten))
        {
            yield return child;
        }
    }

    partial void OnSelectedNodeChanged(InventoryNodeViewModel? value)
        => OnPropertyChanged(nameof(HasSelection));

    partial void OnErrorTextChanged(string? value)
        => OnPropertyChanged(nameof(HasError));

    partial void OnIsCachedChanged(bool value)
        => OnPropertyChanged(nameof(CachedBadgeText));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.StateChanged -= OnConnectionStateChanged;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }
}
