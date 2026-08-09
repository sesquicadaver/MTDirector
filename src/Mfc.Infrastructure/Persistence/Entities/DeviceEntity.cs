namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted Device aggregate (Vertical Slice §8.4).</summary>
public sealed class DeviceEntity
{
    public Guid Id { get; set; }

    public Guid NodeId { get; set; }

    public required string DisplayName { get; set; }

    public required string ManagementHost { get; set; }

    public short ManagementHostKind { get; set; }

    public int ManagementPort { get; set; }

    public bool Enabled { get; set; }

    /// <summary>Persisted <see cref="Mfc.Domain.Inventory.DeviceRole"/> (M1-25).</summary>
    public short Role { get; set; }

    public short? LastSupportState { get; set; }

    public Guid? LastCompletedCaptureId { get; set; }

    public long RowVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
