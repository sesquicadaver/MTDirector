using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Contracts-only ZoneService client (ADR 0005).</summary>
public interface IZoneServiceClient
{
    Task<IReadOnlyList<ZoneDefinition>> ListZoneDefinitionsAsync(
        PolicyOwnerScope? ownerScope = null,
        Guid? ownerId = null,
        CancellationToken cancellationToken = default);

    Task<ZoneDefinition> CreateZoneDefinitionAsync(
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        string key,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task<ZoneDefinition> UpdateZoneDefinitionAsync(
        Guid zoneId,
        ulong expectedRowVersion,
        string? name,
        string? description,
        bool resetDescription,
        CancellationToken cancellationToken = default);

    Task DeleteZoneDefinitionAsync(
        Guid zoneId,
        ulong expectedRowVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NodeZoneBinding>> ListNodeZoneBindingsAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default);

    Task<NodeZoneBinding> UpsertNodeZoneBindingAsync(
        Guid nodeId,
        Guid zoneId,
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        ulong? expectedRowVersion,
        CancellationToken cancellationToken = default);

    Task DeleteNodeZoneBindingAsync(
        Guid bindingId,
        ulong expectedRowVersion,
        CancellationToken cancellationToken = default);

    Task<ZoneResolveBatch> ResolveZonesForNodeAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default);

    Task<ZoneResolveBatch> ResolveZonesForDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);
}
