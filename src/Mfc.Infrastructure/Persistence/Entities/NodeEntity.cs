namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted Node aggregate (Vertical Slice §8.3).</summary>
public sealed class NodeEntity
{
    public Guid Id { get; set; }

    public Guid SiteId { get; set; }

    public required string Name { get; set; }

    public short DeclaredKind { get; set; }

    public short DeclaredUplinkMode { get; set; }

    public short Status { get; set; }

    public long RowVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
