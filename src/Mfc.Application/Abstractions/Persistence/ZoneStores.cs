using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Desired zone definition catalog (Policy Model §20; M2-05).</summary>
public interface IZoneDefinitionStore
{
    Task AddAsync(ZoneDefinition zone, CancellationToken cancellationToken = default);

    Task<ZoneDefinition?> GetAsync(ZoneId id, CancellationToken cancellationToken = default);

    Task UpdateAsync(ZoneDefinition zone, CancellationToken cancellationToken = default);

    Task DeleteAsync(ZoneId id, CancellationToken cancellationToken = default);

    Task<bool> KeyExistsAsync(
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        NonEmptyName key,
        ZoneId? excludingId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZoneDefinition>> ListAsync(
        PolicyOwnerScope? ownerScope = null,
        Guid? ownerId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Desired Node→zone bindings (Policy Model §21; M2-05).</summary>
public interface INodeZoneBindingStore
{
    Task AddAsync(NodeZoneBinding binding, CancellationToken cancellationToken = default);

    Task<NodeZoneBinding?> GetAsync(NodeZoneBindingId id, CancellationToken cancellationToken = default);

    Task<NodeZoneBinding?> GetByNodeAndZoneAsync(
        NodeId nodeId,
        ZoneId zoneId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(NodeZoneBinding binding, CancellationToken cancellationToken = default);

    Task DeleteAsync(NodeZoneBindingId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NodeZoneBinding>> ListByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NodeZoneBinding>> ListByZoneAsync(
        ZoneId zoneId,
        CancellationToken cancellationToken = default);

    Task<int> CountByZoneAsync(ZoneId zoneId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds per-device zone resolve observation from the latest completed capture (Application ports only).
/// </summary>
public interface IZoneResolveObservationSource
{
    Task<ZoneResolveDeviceObservation> GetForDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default);
}
