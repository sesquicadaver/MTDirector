using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>Thin read-only policy rules panel (Contracts-only; M2-06).</summary>
public sealed partial class PoliciesViewModel : ObservableObject, IDisposable
{
    private readonly IPolicyPanelService _policies;
    private readonly IControllerConnectionService _connection;
    private bool _disposed;

    public PoliciesViewModel(IPolicyPanelService policies, IControllerConnectionService connection)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.StateChanged += OnConnectionStateChanged;
    }

    public ObservableCollection<PolicyRuleListItem> Rules { get; } = [];

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _revisionIdText = string.Empty;

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task RefreshAsync()
    {
        if (!Guid.TryParse(RevisionIdText.Trim(), out Guid revisionId))
        {
            ErrorText = "Enter a valid policy revision UUID.";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            IReadOnlyList<PolicyRuleListItem> rules = await _policies
                .ListRulesAsync(revisionId, activeOnly: false, ct)
                .ConfigureAwait(true);
            Rules.Clear();
            foreach (PolicyRuleListItem rule in rules)
            {
                Rules.Add(rule);
            }
        }).ConfigureAwait(true);
    }

    private bool CanOperate()
        => !IsBusy && _connection.State == ControllerConnectionState.Connected;

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
        }
    }

    private void NotifyCommands() => RefreshCommand.NotifyCanExecuteChanged();

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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.StateChanged -= OnConnectionStateChanged;
    }
}
