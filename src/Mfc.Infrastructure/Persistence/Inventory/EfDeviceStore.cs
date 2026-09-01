using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Inventory;

/// <summary>EF Core device aggregate store.</summary>
public sealed class EfDeviceStore : IDeviceStore
{
    private readonly MfcDbContext _db;

    public EfDeviceStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _db.Devices.Add(ToEntity(device, now, now));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Device?> GetAsync(DeviceId id, CancellationToken cancellationToken = default)
    {
        DeviceEntity? entity = await _db.Devices.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        DeviceEntity? entity = await _db.Devices
            .SingleOrDefaultAsync(d => d.Id == device.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            throw new InvalidOperationException($"Device '{device.Id.Value}' was not found for update.");
        }

        entity.DisplayName = device.DisplayName.Value;
        entity.ManagementHost = device.ManagementEndpoint.Host.Value;
        entity.ManagementHostKind = (short)device.ManagementEndpoint.Host.HostKind;
        entity.ManagementPort = device.ManagementEndpoint.Port;
        entity.Enabled = device.Enabled;
        entity.Role = (short)device.Role;
        entity.ManagementState = (short)device.ManagementState;
        entity.LastSupportState = device.LastSupportState is null ? null : (short)device.LastSupportState.Value;
        entity.LastObservedReachability = device.LastObservedReachability is null
            ? null
            : (short)device.LastObservedReachability.Value;
        entity.LastCompletedCaptureId = device.LastCompletedCaptureId;
        entity.RowVersion = (long)device.RowVersion;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Device>> ListByNodeAsync(NodeId nodeId, CancellationToken cancellationToken = default)
    {
        List<DeviceEntity> rows = await _db.Devices.AsNoTracking()
            .Where(d => d.NodeId == nodeId.Value)
            .OrderBy(d => d.DisplayName)
            .ThenBy(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    private static DeviceEntity ToEntity(Device device, DateTimeOffset created, DateTimeOffset updated) => new()
    {
        Id = device.Id.Value,
        NodeId = device.NodeId.Value,
        DisplayName = device.DisplayName.Value,
        ManagementHost = device.ManagementEndpoint.Host.Value,
        ManagementHostKind = (short)device.ManagementEndpoint.Host.HostKind,
        ManagementPort = device.ManagementEndpoint.Port,
        Enabled = device.Enabled,
        Role = (short)device.Role,
        ManagementState = (short)device.ManagementState,
        LastSupportState = device.LastSupportState is null ? null : (short)device.LastSupportState.Value,
        LastObservedReachability = device.LastObservedReachability is null
            ? null
            : (short)device.LastObservedReachability.Value,
        LastCompletedCaptureId = device.LastCompletedCaptureId,
        RowVersion = (long)device.RowVersion,
        CreatedAtUtc = created,
        UpdatedAtUtc = updated,
    };

    private static Device ToDomain(DeviceEntity entity)
        => Device.Reconstitute(
            new DeviceId(entity.Id),
            new NodeId(entity.NodeId),
            NonEmptyName.Create(entity.DisplayName),
            ManagementEndpoint.Create(entity.ManagementHost, (ushort)entity.ManagementPort),
            (DeviceRole)entity.Role,
            entity.Enabled,
            entity.LastSupportState is null ? null : (SupportState)entity.LastSupportState.Value,
            (ManagementState)entity.ManagementState,
            (ulong)entity.RowVersion,
            entity.LastCompletedCaptureId,
            entity.LastObservedReachability is null
                ? null
                : (ObservedReachability)entity.LastObservedReachability.Value);
}
