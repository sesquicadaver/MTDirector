using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Inventory;

/// <summary>EF Core store for <see cref="DeviceHashState"/> (M6-01).</summary>
public sealed class EfDeviceHashStateStore : IDeviceHashStateStore
{
    private readonly MfcDbContext _db;

    public EfDeviceHashStateStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task UpsertAsync(DeviceHashState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        DeviceHashStateEntity? entity = await _db.DeviceHashStates
            .SingleOrDefaultAsync(e => e.DeviceId == state.DeviceId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            _db.DeviceHashStates.Add(ToEntity(state));
        }
        else
        {
            entity.DesiredPolicyHash = ToBytes(state.DesiredPolicyHash);
            entity.DesiredArtifactHash = ToBytes(state.DesiredArtifactHash);
            entity.LastCommittedPolicyHash = ToBytes(state.LastCommittedPolicyHash);
            entity.LastCommittedArtifactHash = ToBytes(state.LastCommittedArtifactHash);
            entity.ActualManagedResourceHash = ToBytes(state.ActualManagedResourceHash);
            entity.ActualKnown = state.ActualKnown;
            entity.AnchorKnown = state.AnchorKnown;
            entity.UpdatedAtUtc = state.UpdatedAtUtc;
            entity.RowVersion = (long)state.RowVersion;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeviceHashState?> GetAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        DeviceHashStateEntity? entity = await _db.DeviceHashStates.AsNoTracking()
            .SingleOrDefaultAsync(e => e.DeviceId == deviceId.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<DeviceHashState>> ListByDeviceIdsAsync(
        IReadOnlyList<DeviceId> deviceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);
        if (deviceIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = deviceIds.Select(static d => d.Value).Distinct().ToArray();
        List<DeviceHashStateEntity> rows = await _db.DeviceHashStates.AsNoTracking()
            .Where(e => ids.Contains(e.DeviceId))
            .OrderBy(e => e.DeviceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    private static DeviceHashStateEntity ToEntity(DeviceHashState state) => new()
    {
        DeviceId = state.DeviceId.Value,
        DesiredPolicyHash = ToBytes(state.DesiredPolicyHash),
        DesiredArtifactHash = ToBytes(state.DesiredArtifactHash),
        LastCommittedPolicyHash = ToBytes(state.LastCommittedPolicyHash),
        LastCommittedArtifactHash = ToBytes(state.LastCommittedArtifactHash),
        ActualManagedResourceHash = ToBytes(state.ActualManagedResourceHash),
        ActualKnown = state.ActualKnown,
        AnchorKnown = state.AnchorKnown,
        UpdatedAtUtc = state.UpdatedAtUtc,
        RowVersion = (long)state.RowVersion,
    };

    private static DeviceHashState ToDomain(DeviceHashStateEntity entity)
        => DeviceHashState.Reconstitute(
            new DeviceId(entity.DeviceId),
            FromBytes(entity.DesiredPolicyHash),
            FromBytes(entity.DesiredArtifactHash),
            FromBytes(entity.LastCommittedPolicyHash),
            FromBytes(entity.LastCommittedArtifactHash),
            FromBytes(entity.ActualManagedResourceHash),
            entity.ActualKnown,
            entity.AnchorKnown,
            entity.UpdatedAtUtc,
            (ulong)entity.RowVersion);

    private static byte[]? ToBytes(Hash256? hash)
        => hash is null ? null : hash.Bytes.ToArray();

    private static Hash256? FromBytes(byte[]? bytes)
        => bytes is null || bytes.Length == 0 ? null : Hash256.Create(bytes);
}
