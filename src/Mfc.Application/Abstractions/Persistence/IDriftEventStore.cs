using Mfc.Domain.Drift;
using Mfc.Domain.Drift.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Append-only store for immutable drift detection events (M6-02).</summary>
public interface IDriftEventStore
{
    Task AppendAsync(DriftEvent driftEvent, CancellationToken cancellationToken = default);

    Task<DriftEvent?> GetAsync(DriftEventId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriftEvent>> ListByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriftEvent>> ListByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when any device on the Node has a latest drift event that blocks deployment (Critical).
    /// </summary>
    Task<bool> HasBlockingCriticalDriftAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default);
}
