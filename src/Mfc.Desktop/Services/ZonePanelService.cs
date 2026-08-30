using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Presentation row for a zone definition.</summary>
public sealed class ZoneDefinitionListItem
{
    public required Guid Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string OwnerScopeText { get; init; }

    public Guid? OwnerId { get; init; }

    public string? Description { get; init; }

    public required ulong RowVersion { get; init; }

    public string SummaryLine => $"{Key} — {Name} ({OwnerScopeText})";
}

/// <summary>Presentation row for a node zone binding.</summary>
public sealed class NodeZoneBindingListItem
{
    public required Guid Id { get; init; }

    public required Guid ZoneId { get; init; }

    public required string KindText { get; init; }

    public required string ValuesText { get; init; }

    public required bool AnalysisStale { get; init; }

    public required ulong RowVersion { get; init; }

    public string SummaryLine => $"{KindText}: {ValuesText}" + (AnalysisStale ? " [stale]" : string.Empty);
}

/// <summary>Presentation row for a resolve blocker / result.</summary>
public sealed class ZoneResolveResultListItem
{
    public required Guid DeviceId { get; init; }

    public required Guid ZoneId { get; init; }

    public required string MembersText { get; init; }

    public required bool AnalysisStale { get; init; }

    public required IReadOnlyList<string> BlockerLines { get; init; }

    public string SummaryLine
    {
        get
        {
            string stale = AnalysisStale ? " stale" : string.Empty;
            string blockers = BlockerLines.Count == 0
                ? "no blockers"
                : string.Join("; ", BlockerLines);
            return $"device={DeviceId:D} zone={ZoneId:D} members=[{MembersText}]{stale} — {blockers}";
        }
    }
}

/// <summary>Desktop zone panel orchestration over Contracts-only client.</summary>
public interface IZonePanelService
{
    Task<IReadOnlyList<ZoneDefinitionListItem>> ListZonesAsync(
        CancellationToken cancellationToken = default);

    Task<ZoneDefinitionListItem> CreateCompanyZoneAsync(
        string key,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task<ZoneDefinitionListItem> UpdateZoneAsync(
        ZoneDefinitionListItem zone,
        string name,
        string? description,
        bool resetDescription,
        CancellationToken cancellationToken = default);

    Task DeleteZoneAsync(
        ZoneDefinitionListItem zone,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NodeZoneBindingListItem>> ListBindingsAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default);

    Task<NodeZoneBindingListItem> UpsertBindingAsync(
        Guid nodeId,
        Guid zoneId,
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        ulong? expectedRowVersion,
        CancellationToken cancellationToken = default);

    Task DeleteBindingAsync(
        NodeZoneBindingListItem binding,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForNodeAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);
}

/// <summary>Default zone panel service.</summary>
public sealed class ZonePanelService : IZonePanelService
{
    private readonly IZoneServiceClient _client;

    public ZonePanelService(IZoneServiceClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IReadOnlyList<ZoneDefinitionListItem>> ListZonesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ZoneDefinition> zones = await _client
            .ListZoneDefinitionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return zones.Select(ToItem).ToArray();
    }

    public async Task<ZoneDefinitionListItem> CreateCompanyZoneAsync(
        string key,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ZoneDefinition zone = await _client.CreateZoneDefinitionAsync(
                PolicyOwnerScope.Company,
                ownerId: null,
                key,
                name,
                description,
                cancellationToken)
            .ConfigureAwait(false);
        return ToItem(zone);
    }

    public async Task<ZoneDefinitionListItem> UpdateZoneAsync(
        ZoneDefinitionListItem zone,
        string name,
        string? description,
        bool resetDescription,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ZoneDefinition updated = await _client.UpdateZoneDefinitionAsync(
                zone.Id,
                zone.RowVersion,
                name,
                description,
                resetDescription,
                cancellationToken)
            .ConfigureAwait(false);
        return ToItem(updated);
    }

    public Task DeleteZoneAsync(ZoneDefinitionListItem zone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return _client.DeleteZoneDefinitionAsync(zone.Id, zone.RowVersion, cancellationToken);
    }

    public async Task<IReadOnlyList<NodeZoneBindingListItem>> ListBindingsAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NodeZoneBinding> bindings = await _client
            .ListNodeZoneBindingsAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);
        return bindings.Select(ToItem).ToArray();
    }

    public async Task<NodeZoneBindingListItem> UpsertBindingAsync(
        Guid nodeId,
        Guid zoneId,
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        ulong? expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        NodeZoneBinding binding = await _client.UpsertNodeZoneBindingAsync(
                nodeId,
                zoneId,
                kind,
                values,
                expectedRowVersion,
                cancellationToken)
            .ConfigureAwait(false);
        return ToItem(binding);
    }

    public Task DeleteBindingAsync(
        NodeZoneBindingListItem binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return _client.DeleteNodeZoneBindingAsync(binding.Id, binding.RowVersion, cancellationToken);
    }

    public async Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForNodeAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        ZoneResolveBatch batch = await _client
            .ResolveZonesForNodeAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);
        return ToResolveItems(batch);
    }

    public async Task<IReadOnlyList<ZoneResolveResultListItem>> ResolveForDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        ZoneResolveBatch batch = await _client
            .ResolveZonesForDeviceAsync(deviceId, cancellationToken)
            .ConfigureAwait(false);
        return ToResolveItems(batch);
    }

    private static ZoneResolveResultListItem[] ToResolveItems(ZoneResolveBatch batch)
        => batch.Results.Select(r => new ZoneResolveResultListItem
        {
            DeviceId = DesktopProtoUuid.ToGuid(r.DeviceId),
            ZoneId = DesktopProtoUuid.ToGuid(r.ZoneId),
            MembersText = string.Join(',', r.ResolvedMembers),
            AnalysisStale = r.AnalysisStale,
            BlockerLines = r.Blockers
                .Select(b => string.IsNullOrWhiteSpace(b.Subject) ? $"{b.Code}: {b.Message}" : $"{b.Code}({b.Subject}): {b.Message}")
                .ToArray(),
        }).ToArray();

    private static ZoneDefinitionListItem ToItem(ZoneDefinition zone) => new()
    {
        Id = DesktopProtoUuid.ToGuid(zone.Id),
        Key = zone.Key,
        Name = zone.Name,
        OwnerScopeText = zone.OwnerScope.ToString(),
        OwnerId = zone.OwnerId is null ? null : DesktopProtoUuid.ToGuid(zone.OwnerId),
        Description = zone.HasDescription ? zone.Description : null,
        RowVersion = zone.RowVersion,
    };

    private static NodeZoneBindingListItem ToItem(NodeZoneBinding binding) => new()
    {
        Id = DesktopProtoUuid.ToGuid(binding.Id),
        ZoneId = DesktopProtoUuid.ToGuid(binding.ZoneId),
        KindText = binding.Kind.ToString(),
        ValuesText = string.Join(',', binding.Values),
        AnalysisStale = binding.AnalysisStale,
        RowVersion = binding.RowVersion,
    };
}
