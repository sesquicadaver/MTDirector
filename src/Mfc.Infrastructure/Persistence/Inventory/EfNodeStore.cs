using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Inventory;

/// <summary>EF Core node aggregate store.</summary>
public sealed class EfNodeStore : INodeStore
{
    private readonly MfcDbContext _db;

    public EfNodeStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<bool> NameExistsAsync(SiteId siteId, NonEmptyName name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _db.Nodes.AsNoTracking()
            .AnyAsync(n => n.SiteId == siteId.Value && n.Name == name.Value, cancellationToken);
    }

    public async Task AddAsync(Node node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _db.Nodes.Add(new NodeEntity
        {
            Id = node.Id.Value,
            SiteId = node.SiteId.Value,
            Name = node.Name.Value,
            DeclaredKind = (short)node.DeclaredKind,
            DeclaredUplinkMode = (short)node.DeclaredUplinkMode,
            Status = (short)node.Status,
            ManagementState = (short)node.ManagementState,
            RowVersion = (long)node.RowVersion,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Node?> GetAsync(NodeId id, CancellationToken cancellationToken = default)
    {
        NodeEntity? entity = await _db.Nodes.AsNoTracking()
            .SingleOrDefaultAsync(n => n.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        Node node = ToDomain(entity);
        List<DeviceEntity> deviceRows = await _db.Devices.AsNoTracking()
            .Where(d => d.NodeId == id.Value)
            .OrderBy(d => d.DisplayName)
            .ThenBy(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (DeviceEntity deviceEntity in deviceRows)
        {
            node.AttachDevice(ToDeviceDomain(deviceEntity));
        }

        return node;
    }

    public async Task UpdateAsync(Node node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        NodeEntity? entity = await _db.Nodes
            .SingleOrDefaultAsync(n => n.Id == node.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            throw new InvalidOperationException($"Node '{node.Id.Value}' was not found for update.");
        }

        entity.Name = node.Name.Value;
        entity.DeclaredKind = (short)node.DeclaredKind;
        entity.DeclaredUplinkMode = (short)node.DeclaredUplinkMode;
        entity.Status = (short)node.Status;
        entity.ManagementState = (short)node.ManagementState;
        entity.RowVersion = (long)node.RowVersion;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Node>> ListBySiteAsync(SiteId siteId, CancellationToken cancellationToken = default)
    {
        List<NodeEntity> rows = await _db.Nodes.AsNoTracking()
            .Where(n => n.SiteId == siteId.Value)
            .OrderBy(n => n.Name)
            .ThenBy(n => n.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    private static Node ToDomain(NodeEntity entity)
        => Node.Reconstitute(
            new NodeId(entity.Id),
            new SiteId(entity.SiteId),
            NonEmptyName.Create(entity.Name),
            (NodeKind)entity.DeclaredKind,
            (DeclaredUplinkMode)entity.DeclaredUplinkMode,
            (NodeStatus)entity.Status,
            (ManagementState)entity.ManagementState,
            (ulong)entity.RowVersion);

    private static Device ToDeviceDomain(DeviceEntity entity)
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
            entity.LastCompletedCaptureId);
}
