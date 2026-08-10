namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted policy container (Policy Model §7 / §66).</summary>
public sealed class PolicyEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public short Kind { get; set; }

    public short OwnerScope { get; set; }

    public Guid? OwnerId { get; set; }

    public short Status { get; set; }

    public long RowVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
