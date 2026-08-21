using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Persists per-device routing assurance state shell (M7.1-02).</summary>
public interface IRoutingAssuranceStateStore
{
    Task UpsertAsync(RoutingAssuranceState state, CancellationToken cancellationToken = default);

    Task<RoutingAssuranceState?> GetAsync(DeviceId deviceId, CancellationToken cancellationToken = default);
}
