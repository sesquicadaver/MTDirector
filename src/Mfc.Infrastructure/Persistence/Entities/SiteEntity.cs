namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted Site aggregate (Vertical Slice §8.2).</summary>
public sealed class SiteEntity
{
    public Guid Id { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public short Status { get; set; }

    public long RowVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
