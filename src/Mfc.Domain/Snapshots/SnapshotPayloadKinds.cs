namespace Mfc.Domain.Snapshots;

/// <summary>Content-addressed payload kinds (Vertical Slice §8.7).</summary>
public enum SnapshotPayloadKind : short
{
    RawSanitized = 1,
    CanonicalConfiguration = 2,
    CanonicalObservations = 3,
    CanonicalCapabilities = 4,
    CanonicalCompatibilityMaterial = 5,
}

/// <summary>Payload compression algorithms. Compression never enters the content hash (M1-23 AC#9).</summary>
public enum SnapshotCompression : short
{
    None = 0,
    Brotli = 1,
}
