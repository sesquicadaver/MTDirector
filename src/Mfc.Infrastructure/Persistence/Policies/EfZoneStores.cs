using System.Text.Json;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Policies;

/// <summary>EF Core zone definition catalog store (M2-05).</summary>
public sealed class EfZoneDefinitionStore : IZoneDefinitionStore
{
    private readonly MfcDbContext _db;

    public EfZoneDefinitionStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AddAsync(ZoneDefinition zone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zone);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _db.ZoneDefinitions.Add(ToEntity(zone, now, now));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ZoneDefinition?> GetAsync(ZoneId id, CancellationToken cancellationToken = default)
    {
        ZoneDefinitionEntity? entity = await _db.ZoneDefinitions.AsNoTracking()
            .SingleOrDefaultAsync(z => z.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(ZoneDefinition zone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ZoneDefinitionEntity? entity = await _db.ZoneDefinitions
            .SingleOrDefaultAsync(z => z.Id == zone.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            throw new InvalidOperationException($"Zone '{zone.Id.Value}' was not found for update.");
        }

        entity.Name = zone.Name.Value;
        entity.Description = zone.Description;
        entity.RowVersion = (long)zone.RowVersion;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(ZoneId id, CancellationToken cancellationToken = default)
    {
        ZoneDefinitionEntity? entity = await _db.ZoneDefinitions
            .SingleOrDefaultAsync(z => z.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        _db.ZoneDefinitions.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> KeyExistsAsync(
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        NonEmptyName key,
        ZoneId? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        short scope = (short)ownerScope;
        IQueryable<ZoneDefinitionEntity> query = _db.ZoneDefinitions.AsNoTracking()
            .Where(z => z.OwnerScope == scope && z.Key == key.Value);
        query = ownerId is null
            ? query.Where(z => z.OwnerId == null)
            : query.Where(z => z.OwnerId == ownerId);
        if (excludingId is not null)
        {
            Guid exclude = excludingId.Value.Value;
            query = query.Where(z => z.Id != exclude);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ZoneDefinition>> ListAsync(
        PolicyOwnerScope? ownerScope = null,
        Guid? ownerId = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ZoneDefinitionEntity> query = _db.ZoneDefinitions.AsNoTracking();
        if (ownerScope is not null)
        {
            short scope = (short)ownerScope.Value;
            query = query.Where(z => z.OwnerScope == scope);
            query = ownerId is null
                ? query.Where(z => z.OwnerId == null)
                : query.Where(z => z.OwnerId == ownerId);
        }

        List<ZoneDefinitionEntity> rows = await query
            .OrderBy(z => z.OwnerScope)
            .ThenBy(z => z.OwnerId)
            .ThenBy(z => z.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    private static ZoneDefinitionEntity ToEntity(
        ZoneDefinition zone,
        DateTimeOffset created,
        DateTimeOffset updated) => new()
    {
        Id = zone.Id.Value,
        OwnerScope = (short)zone.OwnerScope,
        OwnerId = zone.OwnerId,
        Key = zone.Key.Value,
        Name = zone.Name.Value,
        Description = zone.Description,
        RowVersion = (long)zone.RowVersion,
        CreatedAtUtc = created,
        UpdatedAtUtc = updated,
    };

    private static ZoneDefinition ToDomain(ZoneDefinitionEntity entity)
        => ZoneDefinition.Reconstitute(
            new ZoneId(entity.Id),
            (PolicyOwnerScope)entity.OwnerScope,
            entity.OwnerId,
            NonEmptyName.Create(entity.Key),
            NonEmptyName.Create(entity.Name),
            entity.Description,
            (ulong)entity.RowVersion);
}

/// <summary>EF Core node zone binding store (M2-05).</summary>
public sealed class EfNodeZoneBindingStore : INodeZoneBindingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly MfcDbContext _db;

    public EfNodeZoneBindingStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AddAsync(NodeZoneBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _db.NodeZoneBindings.Add(ToEntity(binding, now, now));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<NodeZoneBinding?> GetAsync(
        NodeZoneBindingId id,
        CancellationToken cancellationToken = default)
    {
        NodeZoneBindingEntity? entity = await _db.NodeZoneBindings.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<NodeZoneBinding?> GetByNodeAndZoneAsync(
        NodeId nodeId,
        ZoneId zoneId,
        CancellationToken cancellationToken = default)
    {
        NodeZoneBindingEntity? entity = await _db.NodeZoneBindings.AsNoTracking()
            .SingleOrDefaultAsync(
                b => b.NodeId == nodeId.Value && b.ZoneId == zoneId.Value,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(NodeZoneBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        NodeZoneBindingEntity? entity = await _db.NodeZoneBindings
            .SingleOrDefaultAsync(b => b.Id == binding.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            throw new InvalidOperationException($"Binding '{binding.Id.Value}' was not found for update.");
        }

        entity.Kind = (short)binding.Kind;
        entity.ValuesJson = SerializeValues(binding.Values);
        entity.ExpectedDependencyHash = binding.ExpectedDependencyHash.Bytes.ToArray();
        entity.LastResolvedDependencyHash = binding.LastResolvedDependencyHash?.Bytes.ToArray();
        entity.AnalysisStale = binding.AnalysisStale;
        entity.RowVersion = (long)binding.RowVersion;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(NodeZoneBindingId id, CancellationToken cancellationToken = default)
    {
        NodeZoneBindingEntity? entity = await _db.NodeZoneBindings
            .SingleOrDefaultAsync(b => b.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        _db.NodeZoneBindings.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NodeZoneBinding>> ListByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
    {
        List<NodeZoneBindingEntity> rows = await _db.NodeZoneBindings.AsNoTracking()
            .Where(b => b.NodeId == nodeId.Value)
            .OrderBy(b => b.ZoneId)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<NodeZoneBinding>> ListByZoneAsync(
        ZoneId zoneId,
        CancellationToken cancellationToken = default)
    {
        List<NodeZoneBindingEntity> rows = await _db.NodeZoneBindings.AsNoTracking()
            .Where(b => b.ZoneId == zoneId.Value)
            .OrderBy(b => b.NodeId)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<int> CountByZoneAsync(ZoneId zoneId, CancellationToken cancellationToken = default)
        => await _db.NodeZoneBindings.AsNoTracking()
            .CountAsync(b => b.ZoneId == zoneId.Value, cancellationToken)
            .ConfigureAwait(false);

    private static NodeZoneBindingEntity ToEntity(
        NodeZoneBinding binding,
        DateTimeOffset created,
        DateTimeOffset updated) => new()
    {
        Id = binding.Id.Value,
        NodeId = binding.NodeId.Value,
        ZoneId = binding.ZoneId.Value,
        Kind = (short)binding.Kind,
        ValuesJson = SerializeValues(binding.Values),
        ExpectedDependencyHash = binding.ExpectedDependencyHash.Bytes.ToArray(),
        LastResolvedDependencyHash = binding.LastResolvedDependencyHash?.Bytes.ToArray(),
        AnalysisStale = binding.AnalysisStale,
        RowVersion = (long)binding.RowVersion,
        CreatedAtUtc = created,
        UpdatedAtUtc = updated,
    };

    private static NodeZoneBinding ToDomain(NodeZoneBindingEntity entity)
        => NodeZoneBinding.Reconstitute(
            new NodeZoneBindingId(entity.Id),
            new NodeId(entity.NodeId),
            new ZoneId(entity.ZoneId),
            (NodeZoneBindingKind)entity.Kind,
            DeserializeValues(entity.ValuesJson),
            Hash256.Create(entity.ExpectedDependencyHash),
            entity.LastResolvedDependencyHash is null
                ? null
                : Hash256.Create(entity.LastResolvedDependencyHash),
            entity.AnalysisStale,
            (ulong)entity.RowVersion);

    private static string SerializeValues(IReadOnlyList<string> values)
        => JsonSerializer.Serialize(values, JsonOptions);

    private static string[] DeserializeValues(string json)
    {
        string[]? values = JsonSerializer.Deserialize<string[]>(json, JsonOptions);
        if (values is null || values.Length == 0)
        {
            throw new InvalidOperationException("Persisted binding values JSON is empty.");
        }

        return values;
    }
}
