namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Content-addressed snapshot payload body. Raw and canonical kinds are separate rows (Vertical Slice §8.7).
/// </summary>
public sealed class SnapshotPayloadEntity
{
    public required byte[] PayloadHash { get; set; }

    public short PayloadKind { get; set; }

    public int SchemaVersion { get; set; }

    public short Compression { get; set; }

    public long UncompressedSize { get; set; }

    public required byte[] CompressedPayload { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
