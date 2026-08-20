using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Persists per-device desired / committed / actual hash projections (M6-01).</summary>
public interface IDeviceHashStateStore
{
    Task UpsertAsync(DeviceHashState state, CancellationToken cancellationToken = default);

    Task<DeviceHashState?> GetAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceHashState>> ListByDeviceIdsAsync(
        IReadOnlyList<DeviceId> deviceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devices with a last_committed artifact hash, bounded for global drift polling (M6-03).
    /// No per-device schedules — callers apply one global poll configuration.
    /// </summary>
    Task<IReadOnlyList<DeviceHashState>> ListWithLastCommittedAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
