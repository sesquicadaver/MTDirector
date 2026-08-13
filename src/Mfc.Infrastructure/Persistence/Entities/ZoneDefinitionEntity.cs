namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted desired zone definition (Policy Model §20; M2-05).</summary>
public sealed class ZoneDefinitionEntity
{
    public Guid Id { get; set; }

    public short OwnerScope { get; set; }

    public Guid? OwnerId { get; set; }

    public required string Key { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public long RowVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
