using System.Text.Json;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Drift;
using Mfc.Domain.Drift.Primitives;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Drift;

/// <summary>EF Core append-only store for <see cref="DriftEvent"/> (M6-02).</summary>
public sealed class EfDriftEventStore : IDriftEventStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly MfcDbContext _db;

    public EfDriftEventStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AppendAsync(DriftEvent driftEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(driftEvent);
        _db.DriftEvents.Add(ToEntity(driftEvent));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DriftEvent?> GetAsync(DriftEventId id, CancellationToken cancellationToken = default)
    {
        DriftEventEntity? entity = await _db.DriftEvents.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<DriftEvent>> ListByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        List<DriftEventEntity> rows = await _db.DriftEvents.AsNoTracking()
            .Where(e => e.DeviceId == deviceId.Value)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<DriftEvent>> ListByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
    {
        List<DriftEventEntity> rows = await _db.DriftEvents.AsNoTracking()
            .Where(e => e.NodeId == nodeId.Value)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<bool> HasBlockingCriticalDriftAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
    {
        // Latest event per device on the Node — any still-blocking Critical outcome blocks deploy.
        List<Guid> deviceIds = await _db.DriftEvents.AsNoTracking()
            .Where(e => e.NodeId == nodeId.Value)
            .Select(e => e.DeviceId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (deviceIds.Count == 0)
        {
            return false;
        }

        foreach (Guid deviceId in deviceIds)
        {
            DriftEventEntity? latest = await _db.DriftEvents.AsNoTracking()
                .Where(e => e.DeviceId == deviceId)
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (latest is { BlocksDeployment: true })
            {
                return true;
            }
        }

        return false;
    }

    private static DriftEventEntity ToEntity(DriftEvent driftEvent) => new()
    {
        Id = driftEvent.Id.Value,
        DeviceId = driftEvent.DeviceId.Value,
        NodeId = driftEvent.NodeId.Value,
        BaselineCommittedHash = ToBytes(driftEvent.BaselineCommittedHash),
        ActualManagedResourceHash = ToBytes(driftEvent.ActualManagedResourceHash),
        DesiredArtifactHashIgnoredForBaseline = ToBytes(driftEvent.DesiredArtifactHashIgnoredForBaseline),
        Outcome = (short)driftEvent.Outcome,
        ConfigurationDriftPresent = driftEvent.ConfigurationDriftPresent,
        BlocksDeployment = driftEvent.BlocksDeployment,
        FindingsJson = JsonSerializer.Serialize(
            driftEvent.Findings.Select(static f => new FindingDto
            {
                Kind = (byte)f.Kind,
                Severity = (byte)f.Severity,
                Detail = f.Detail,
            }).ToArray(),
            JsonOptions),
        SemanticDiffCanonical = driftEvent.SemanticDiffCanonical,
        SemanticDiffHash = ToBytes(driftEvent.SemanticDiffHash),
        CreatedAtUtc = driftEvent.CreatedAtUtc,
        Immutable = true,
    };

    private static DriftEvent ToDomain(DriftEventEntity entity)
    {
        FindingDto[] dtos = JsonSerializer.Deserialize<FindingDto[]>(entity.FindingsJson, JsonOptions) ?? [];
        List<DriftFinding> findings = new(dtos.Length);
        foreach (FindingDto dto in dtos)
        {
            findings.Add(new DriftFinding((DriftFindingKind)dto.Kind, dto.Detail));
        }

        return DriftEvent.Reconstitute(
            new DriftEventId(entity.Id),
            new DeviceId(entity.DeviceId),
            new NodeId(entity.NodeId),
            FromBytes(entity.BaselineCommittedHash),
            FromBytes(entity.ActualManagedResourceHash),
            FromBytes(entity.DesiredArtifactHashIgnoredForBaseline),
            (DriftOutcome)entity.Outcome,
            entity.ConfigurationDriftPresent,
            entity.BlocksDeployment,
            findings,
            entity.SemanticDiffCanonical,
            FromBytes(entity.SemanticDiffHash),
            entity.CreatedAtUtc);
    }

    private static byte[]? ToBytes(Hash256? hash)
        => hash is null ? null : hash.Bytes.ToArray();

    private static Hash256? FromBytes(byte[]? bytes)
        => bytes is null || bytes.Length == 0 ? null : Hash256.Create(bytes);

    private sealed class FindingDto
    {
        public byte Kind { get; set; }

        public byte Severity { get; set; }

        public string? Detail { get; set; }
    }
}
