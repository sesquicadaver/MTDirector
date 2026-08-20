using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>
/// Builds Site→Node→Device presentation tree from InventoryService.
/// Single-flight refresh, cancellation-aware, preserves last successful tree on failure.
/// </summary>
public sealed class InventoryTreeService : IInventoryTreeService
{
    private readonly IInventoryTreeClient _client;
    private readonly object _gate = new();
    private Task<InventoryTreeLoadResult>? _inFlight;
    private IReadOnlyList<InventoryTreeItem> _lastSuccessfulRoots = [];
    private bool _hasSuccessfulLoad;

    public InventoryTreeService(IInventoryTreeClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public InventoryTreeLoadResult Current { get; private set; } = new()
    {
        Roots = [],
        Succeeded = false,
        IsCached = false,
        IsRefreshing = false,
    };

    public Task<InventoryTreeLoadResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Task<InventoryTreeLoadResult> task;
        lock (_gate)
        {
            if (_inFlight is not null)
            {
                // Coalesce: do not start a second overlapping load (AC#5).
                task = _inFlight;
            }
            else
            {
                task = RefreshCoreAsync(cancellationToken);
                _inFlight = task;
                _ = task.ContinueWith(
                    static (finished, state) =>
                    {
                        InventoryTreeService self = (InventoryTreeService)state!;
                        lock (self._gate)
                        {
                            if (ReferenceEquals(self._inFlight, finished))
                            {
                                self._inFlight = null;
                            }
                        }
                    },
                    this,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        return AwaitSharedAsync(task, cancellationToken);
    }

    private static async Task<InventoryTreeLoadResult> AwaitSharedAsync(
        Task<InventoryTreeLoadResult> shared,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return await shared.ConfigureAwait(false);
        }

        TaskCompletionSource<InventoryTreeLoadResult> tcs = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using CancellationTokenRegistration registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<InventoryTreeLoadResult>)state!).TrySetCanceled(),
            tcs);
        Task completed = await Task.WhenAny(shared, tcs.Task).ConfigureAwait(false);
        if (ReferenceEquals(completed, tcs.Task))
        {
            // Wait for the shared load to settle so Current.IsRefreshing is cleared before
            // surfacing cancellation to the caller (avoids racing the OCE catch in RefreshCoreAsync).
            try
            {
                _ = await shared.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the shared work observes the same cancelled token.
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        return await shared.ConfigureAwait(false);
    }

    private async Task<InventoryTreeLoadResult> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        Current = new InventoryTreeLoadResult
        {
            Roots = _lastSuccessfulRoots,
            Succeeded = _hasSuccessfulLoad,
            Error = Current.Error,
            IsCached = _hasSuccessfulLoad,
            IsRefreshing = true,
        };

        try
        {
            List<InventoryTreeItem> roots = await LoadTreeAsync(cancellationToken).ConfigureAwait(false);
            _lastSuccessfulRoots = roots;
            _hasSuccessfulLoad = true;
            Current = new InventoryTreeLoadResult
            {
                Roots = roots,
                Succeeded = true,
                Error = null,
                IsCached = false,
                IsRefreshing = false,
            };
            return Current;
        }
        catch (OperationCanceledException)
        {
            Current = new InventoryTreeLoadResult
            {
                Roots = _lastSuccessfulRoots,
                Succeeded = _hasSuccessfulLoad,
                Error = _hasSuccessfulLoad ? "Refresh cancelled." : "Refresh cancelled.",
                IsCached = _hasSuccessfulLoad,
                IsRefreshing = false,
            };
            throw;
        }
        catch (Exception ex)
        {
            Current = new InventoryTreeLoadResult
            {
                Roots = _lastSuccessfulRoots,
                Succeeded = false,
                Error = ex.Message,
                IsCached = _hasSuccessfulLoad,
                IsRefreshing = false,
            };
            return Current;
        }
    }

    private async Task<List<InventoryTreeItem>> LoadTreeAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Site> sites = await _client.ListAllSitesAsync(cancellationToken).ConfigureAwait(false);
        List<InventoryTreeItem> roots = new(sites.Count);
        foreach (Site site in sites.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Guid siteId = DesktopProtoUuid.ToGuid(site.Id);
            IReadOnlyList<Node> nodes = await _client.ListAllNodesAsync(siteId, cancellationToken)
                .ConfigureAwait(false);
            List<InventoryTreeItem> nodeItems = new(nodes.Count);
            foreach (Node node in nodes.OrderBy(n => n.Name, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Guid nodeId = DesktopProtoUuid.ToGuid(node.Id);
                NodeDetails details = await _client.GetNodeAsync(nodeId, cancellationToken).ConfigureAwait(false);
                List<InventoryTreeItem> devices = details.Devices
                    .OrderBy(d => d.DisplayName, StringComparer.Ordinal)
                    .Select(MapDevice)
                    .ToList();
                nodeItems.Add(new InventoryTreeItem
                {
                    Kind = InventoryTreeKind.Node,
                    Id = nodeId,
                    DisplayName = node.Name,
                    StatusText = FormatEnum(node.Status),
                    NodeKindText = FormatEnum(node.DeclaredKind),
                    UplinkModeText = FormatEnum(node.DeclaredUplinkMode),
                    WorkflowStatusText = FormatEnum(details.WorkflowStatus),
                    Children = devices,
                });
            }

            roots.Add(new InventoryTreeItem
            {
                Kind = InventoryTreeKind.Site,
                Id = siteId,
                DisplayName = string.IsNullOrWhiteSpace(site.Name) ? site.Code : $"{site.Code} — {site.Name}",
                StatusText = FormatEnum(site.Status),
                Children = nodeItems,
            });
        }

        return roots;
    }

    private static InventoryTreeItem MapDevice(Device device)
    {
        string version = device.HasRouterosVersion && !string.IsNullOrWhiteSpace(device.RouterosVersion)
            ? device.RouterosVersion
            : "—";
        string model = device.HasModel && !string.IsNullOrWhiteSpace(device.Model)
            ? device.Model
            : "—";
        string reachability = device.HasReachability && !string.IsNullOrWhiteSpace(device.Reachability)
            ? device.Reachability
            : "Unknown";
        string vrrp = device.VrrpRoleLabels.Count > 0
            ? string.Join(", ", device.VrrpRoleLabels)
            : "—";
        string lastSnapshot = device.LastSnapshotAt is not null
            ? device.LastSnapshotAt.ToDateTimeOffset().UtcDateTime.ToString("u")
            : "—";

        return new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = DesktopProtoUuid.ToGuid(device.Id),
            DisplayName = device.DisplayName,
            StatusText = device.Enabled ? "Enabled" : "Disabled",
            SupportStateText = FormatEnum(device.LastSupportState),
            ReachabilityText = reachability,
            RouterOsVersionText = version,
            ModelText = model,
            VrrpRolesText = vrrp,
            LastSnapshotText = lastSnapshot,
            DesiredHashText = FormatHash(device.DesiredArtifactHash),
            CommittedHashText = FormatHash(device.LastCommittedArtifactHash),
            ActualHashText = FormatHash(device.ActualManagedResourceHash),
        };
    }

    private static string FormatHash(Sha256? hash)
    {
        if (hash is null || hash.Value.Length == 0)
        {
            return "—";
        }

        string hex = Convert.ToHexString(hash.Value.Span).ToLowerInvariant();
        return hex.Length <= 12 ? hex : hex[..12] + "…";
    }

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
}
