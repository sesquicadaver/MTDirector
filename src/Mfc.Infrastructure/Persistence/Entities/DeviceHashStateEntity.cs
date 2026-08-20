namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted desired / committed / actual hashes per Device (M6-01). No workflow status column.</summary>
public sealed class DeviceHashStateEntity
{
    public Guid DeviceId { get; set; }

    public byte[]? DesiredPolicyHash { get; set; }

    public byte[]? DesiredArtifactHash { get; set; }

    public byte[]? LastCommittedPolicyHash { get; set; }

    public byte[]? LastCommittedArtifactHash { get; set; }

    public byte[]? ActualManagedResourceHash { get; set; }

    public bool ActualKnown { get; set; }

    public bool AnchorKnown { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public long RowVersion { get; set; }
}
