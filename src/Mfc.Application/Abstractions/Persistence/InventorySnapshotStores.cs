using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;

namespace Mfc.Application.Abstractions.Persistence;

public interface ISiteStore
{
    Task<bool> CodeExistsAsync(SiteCode code, CancellationToken cancellationToken = default);

    Task AddAsync(Site site, CancellationToken cancellationToken = default);

    Task<Site?> GetAsync(SiteId id, CancellationToken cancellationToken = default);
}

public interface INodeStore
{
    Task<bool> NameExistsAsync(SiteId siteId, NonEmptyName name, CancellationToken cancellationToken = default);

    Task AddAsync(Node node, CancellationToken cancellationToken = default);

    Task<Node?> GetAsync(NodeId id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Node node, CancellationToken cancellationToken = default);
}

public interface IDeviceStore
{
    Task AddAsync(Device device, CancellationToken cancellationToken = default);

    Task<Device?> GetAsync(DeviceId id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Device device, CancellationToken cancellationToken = default);
}

/// <summary>Snapshot metadata plus capture schema version for application persistence.</summary>
public sealed class StoredSnapshot
{
    public required SnapshotMetadata Metadata { get; init; }

    public required int SchemaVersion { get; init; }
}

public interface ISnapshotStore
{
    Task<StoredSnapshot?> GetAsync(SnapshotId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredSnapshot>> ListByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default);

    Task<StoredSnapshot?> FindCompletedBySnapshotHashAsync(
        DeviceId deviceId,
        SnapshotHash snapshotHash,
        CancellationToken cancellationToken = default);

    Task AddAsync(StoredSnapshot snapshot, CancellationToken cancellationToken = default);
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
