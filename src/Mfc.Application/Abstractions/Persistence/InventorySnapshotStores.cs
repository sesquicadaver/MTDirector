using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;

namespace Mfc.Application.Abstractions.Persistence;

public interface ISiteStore
{
    Task<bool> CodeExistsAsync(SiteCode code, CancellationToken cancellationToken = default);

    Task AddAsync(Site site, CancellationToken cancellationToken = default);

    Task<Site?> GetAsync(SiteId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Site>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Cursor page ordered by code ascending, then id ascending.</summary>
    Task<SitePage> ListPageAsync(int limit, string? cursor, CancellationToken cancellationToken = default);
}

/// <summary>Cursor page of sites.</summary>
public sealed class SitePage
{
    public required IReadOnlyList<Site> Items { get; init; }

    public string? NextCursor { get; init; }
}

public interface INodeStore
{
    Task<bool> NameExistsAsync(SiteId siteId, NonEmptyName name, CancellationToken cancellationToken = default);

    Task AddAsync(Node node, CancellationToken cancellationToken = default);

    Task<Node?> GetAsync(NodeId id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Node node, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Node>> ListBySiteAsync(SiteId siteId, CancellationToken cancellationToken = default);
}

public interface IDeviceStore
{
    Task AddAsync(Device device, CancellationToken cancellationToken = default);

    Task<Device?> GetAsync(DeviceId id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Device device, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Device>> ListByNodeAsync(NodeId nodeId, CancellationToken cancellationToken = default);
}

/// <summary>Durable mutation idempotency (idempotency_records).</summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Returns a previously stored resource id when the same actor/operation/key was completed.
    /// When the key exists with a different request hash, returns conflict=true.
    /// </summary>
    Task<IdempotencyLookupResult> TryGetAsync(
        string actor,
        string operation,
        Guid idempotencyKey,
        ReadOnlyMemory<byte> requestHash,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string actor,
        string operation,
        Guid idempotencyKey,
        ReadOnlyMemory<byte> requestHash,
        Guid resourceId,
        CancellationToken cancellationToken = default);
}

public sealed class IdempotencyLookupResult
{
    public bool Found { get; init; }

    public bool Conflict { get; init; }

    public Guid? ResourceId { get; init; }
}

/// <summary>Snapshot metadata plus capture schema version for application persistence.</summary>
public sealed class StoredSnapshot
{
    public required SnapshotMetadata Metadata { get; init; }

    public required int SchemaVersion { get; init; }

    public Guid? OperationId { get; init; }

    public Hash256? RawPayloadHash { get; init; }

    public Hash256? ConfigurationPayloadHash { get; init; }

    public Hash256? ObservationPayloadHash { get; init; }

    public Hash256? CapabilityPayloadHash { get; init; }
}

/// <summary>Cursor page of device snapshots (M1-23 AC#6).</summary>
public sealed class StoredSnapshotPage
{
    public required IReadOnlyList<StoredSnapshot> Items { get; init; }

    public string? NextCursor { get; init; }
}

/// <summary>Decompressed content-addressed payload with integrity-verified hash.</summary>
public sealed class StoredSnapshotPayload
{
    public required Hash256 PayloadHash { get; init; }

    public required SnapshotPayloadKind Kind { get; init; }

    public required int SchemaVersion { get; init; }

    public required SnapshotCompression Compression { get; init; }

    public required ReadOnlyMemory<byte> UncompressedBytes { get; init; }
}

/// <summary>Atomic persist request for a new completed capture (M1-23).</summary>
public sealed class SnapshotPersistRequest
{
    public required DeviceId DeviceId { get; init; }

    public required Guid RequestedBy { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required SnapshotCaptureResult Capture { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }
}

public interface ISnapshotStore
{
    Task<StoredSnapshot?> GetAsync(SnapshotId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredSnapshot>> ListByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>Paginated listing ordered by completed_at desc, id desc.</summary>
    Task<StoredSnapshotPage> ListByDevicePageAsync(
        DeviceId deviceId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<StoredSnapshot?> FindCompletedBySnapshotHashAsync(
        DeviceId deviceId,
        SnapshotHash snapshotHash,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a completed capture linked to an idempotent capture operation, if any.</summary>
    Task<StoredSnapshot?> FindByIdempotencyAsync(
        Guid requestedBy,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically inserts payloads (ON CONFLICT DO NOTHING), sections, capture operation, and completed capture.
    /// </summary>
    Task<StoredSnapshot> PersistCompletedAsync(
        SnapshotPersistRequest request,
        CancellationToken cancellationToken = default);

    Task<StoredSnapshotPayload?> GetPayloadAsync(
        Hash256 payloadHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads parsed canonical sections for a completed capture (configuration + observation payloads).
    /// </summary>
    Task<IReadOnlyList<CanonicalSection>> LoadCanonicalSectionsAsync(
        SnapshotId id,
        CancellationToken cancellationToken = default);
}

/// <summary>Loads connection profile fields needed to open a RouterOS read target (no password).</summary>
public interface IConnectionProfileReadStore
{
    Task<ConnectionProfileReadModel?> GetAsync(DeviceId deviceId, CancellationToken cancellationToken = default);
}

public sealed class ConnectionProfileReadModel
{
    public required SecretReference SecretReference { get; init; }

    public required CertificateTrustMode TrustMode { get; init; }

    public string? CaProfileRef { get; init; }

    public Hash256? PinnedSpkiSha256 { get; init; }
}
