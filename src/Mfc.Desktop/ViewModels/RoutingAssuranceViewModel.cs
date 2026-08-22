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
/// Routing assurance sub-panel under Node module (M7.1-10).
/// Read-only expectations/findings/trace summaries — no routing writes.
/// </summary>
public sealed partial class RoutingAssuranceViewModel : ObservableObject, IDisposable
{
    private readonly IRoutingAssuranceServiceClient _client;
    private readonly IControllerConnectionService _connection;
    private readonly InventoryTreeViewModel _inventory;
    private CancellationTokenSource? _loadCts;
    private bool _disposed;

    public RoutingAssuranceViewModel(
        IRoutingAssuranceServiceClient client,
        IControllerConnectionService connection,
        InventoryTreeViewModel inventory)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _connection.StateChanged += OnConnectionStateChanged;
        _inventory.PropertyChanged += OnInventoryPropertyChanged;
    }

    public ObservableCollection<string> ExpectationLines { get; } = [];

    public ObservableCollection<string> FindingLines { get; } = [];

    public ObservableCollection<string> TraceSummaryLines { get; } = [];

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    /// <summary>Living Spec / AC#3: no routing write surface on Desktop.</summary>
    public bool HasRoutingWriteControls
    {
        get
        {
            _ = _client;
            return false;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Select a Device, then refresh routing assurance.";

    [ObservableProperty]
    private string _configurationHashText = "—";

    [ObservableProperty]
    private string _operationalHashText = "—";

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller before loading routing assurance.";
            return;
        }

        Guid? deviceId = ResolveDeviceId();
        if (deviceId is null)
        {
            ErrorText = "Select a Device in the inventory tree.";
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        CancellationToken token = _loadCts.Token;

        IsBusy = true;
        RefreshCommand.NotifyCanExecuteChanged();
        try
        {
            RoutingAssuranceStateDetail detail = await Task.Run(
                    async () => await _client.GetDeviceRoutingAssuranceStateAsync(deviceId.Value, token)
                        .ConfigureAwait(false),
                    token)
                .ConfigureAwait(true);

            ConfigurationHashText = FormatHash(detail.ConfigurationHash);
            OperationalHashText = FormatHash(detail.OperationalHash);
            ExpectationLines.Clear();
            foreach (RouteExpectation expectation in detail.Expectations)
            {
                ExpectationLines.Add(RouteExpectationLineItem.FromProto(expectation).SummaryLine);
            }

            FindingLines.Clear();
            foreach (RouteFinding finding in detail.Findings)
            {
                FindingLines.Add(RouteFindingLineItem.FromProto(finding).SummaryLine);
            }

            TraceSummaryLines.Clear();
            foreach (RouteResolutionTraceSummary trace in detail.TraceSummaries)
            {
                TraceSummaryLines.Add(RouteResolutionTraceSummaryLineItem.FromProto(trace).SummaryLine);
            }

            StatusText =
                $"Loaded routing assurance for device {deviceId.Value:D}: " +
                $"expectations={detail.RouteExpectationCount}, findings={detail.RouteFindingCount}, " +
                $"traces={detail.ResolutionTraceCount}.";
            ErrorText = null;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Routing assurance refresh cancelled.";
        }
        catch (RpcException ex)
        {
            ErrorText = ex.Status.Detail;
            StatusText = "Routing assurance load failed.";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Routing assurance load failed.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRefresh()
        => !IsBusy && _connection.State == ControllerConnectionState.Connected;

    private Guid? ResolveDeviceId()
    {
        InventoryNodeViewModel? selected = _inventory.SelectedNode;
        if (selected?.Kind == InventoryTreeKind.Device)
        {
            return selected.Id;
        }

        return null;
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshCommand.NotifyCanExecuteChanged();
        }
        else
        {
            Dispatcher.UIThread.Post(() => RefreshCommand.NotifyCanExecuteChanged());
        }
    }

    private void OnInventoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InventoryTreeViewModel.SelectedNode))
        {
            RefreshCommand.NotifyCanExecuteChanged();
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
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }

    private static string FormatHash(Sha256? hash)
    {
        if (hash is null || hash.Value.Length == 0)
        {
            return "—";
        }

        string hex = Convert.ToHexString(hash.Value.Span).ToLowerInvariant();
        return hex.Length <= 16 ? hex : hex[..16] + "…";
    }
}

/// <summary>Presentation row for a route expectation (Contracts-only).</summary>
public sealed class RouteExpectationLineItem
{
    public required string SummaryLine { get; init; }

    public static RouteExpectationLineItem FromProto(RouteExpectation expectation)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        string critical = expectation.Critical ? "critical" : "normal";
        return new RouteExpectationLineItem
        {
            SummaryLine =
                $"{expectation.Family} {expectation.DestinationPrefix} → " +
                $"table={OrDash(expectation.ExpectedTable)} vrf={OrDash(expectation.ExpectedVrf)} " +
                $"next_hops={expectation.AllowedNextHops.Count} egress_if={expectation.AllowedEgressInterfaces.Count} " +
                $"[{critical}]",
        };
    }

    private static string OrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;
}

/// <summary>Presentation row for a route finding (Contracts-only).</summary>
public sealed class RouteFindingLineItem
{
    public required string SummaryLine { get; init; }

    public static RouteFindingLineItem FromProto(RouteFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return new RouteFindingLineItem
        {
            SummaryLine = $"{finding.Code} · subject={OrDash(finding.Subject)} · {finding.Message}",
        };
    }

    private static string OrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;
}

/// <summary>Presentation row for a bounded trace summary (Contracts-only).</summary>
public sealed class RouteResolutionTraceSummaryLineItem
{
    public required string SummaryLine { get; init; }

    public static RouteResolutionTraceSummaryLineItem FromProto(RouteResolutionTraceSummary trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        string nextHops = trace.NextHopGateways.Count == 0
            ? "—"
            : string.Join(",", trace.NextHopGateways);
        string egress = trace.EgressInterfaces.Count == 0
            ? "—"
            : string.Join(",", trace.EgressInterfaces);
        string drift = trace.DriftCodes.Count == 0 ? "—" : string.Join(",", trace.DriftCodes);
        string latency = trace.LatencyCodes.Count == 0 ? "—" : string.Join(",", trace.LatencyCodes);
        return new RouteResolutionTraceSummaryLineItem
        {
            SummaryLine =
                $"{trace.Family} dst={OrDash(trace.DestinationAddress)} " +
                $"table={OrDash(trace.SelectedTable)} vrf={OrDash(trace.SelectedVrf)} " +
                $"prefix={OrDash(trace.MatchedPrefix)} nh={nextHops} egress={egress} " +
                $"path={OrDash(trace.ExecutionPath)} decision={OrDash(trace.Decision)} " +
                $"drift={drift} latency={latency} rev={OrDash(trace.ReversePathSymmetryResult)}",
        };
    }

    private static string OrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
