using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Abstractions.Secrets;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;

namespace Mfc.UnitTests.Application.Fakes;

internal sealed class FakeAuthorizationBoundary : IAuthorizationBoundary
{
    public HashSet<string> DeniedPermissions { get; } = new(StringComparer.Ordinal);

    public Task EnsureAllowedAsync(string actor, string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (DeniedPermissions.Contains(permission))
        {
            throw new UnauthorizedAccessException($"Actor '{actor}' is not allowed '{permission}'.");
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeSiteStore : ISiteStore
{
    private readonly Dictionary<Guid, Site> _byId = [];
    private readonly HashSet<string> _codes = new(StringComparer.Ordinal);

    public Task<bool> CodeExistsAsync(SiteCode code, CancellationToken cancellationToken = default)
        => Task.FromResult(_codes.Contains(code.Value));

    public Task AddAsync(Site site, CancellationToken cancellationToken = default)
    {
        _byId[site.Id.Value] = site;
        _codes.Add(site.Code.Value);
        return Task.CompletedTask;
    }

    public Task<Site?> GetAsync(SiteId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id.Value, out Site? site) ? site : null);
}

internal sealed class FakeNodeStore : INodeStore
{
    private readonly Dictionary<Guid, Node> _byId = [];

    public Task<bool> NameExistsAsync(SiteId siteId, NonEmptyName name, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.Values.Any(n => n.SiteId == siteId && n.Name.Equals(name)));

    public Task AddAsync(Node node, CancellationToken cancellationToken = default)
    {
        _byId[node.Id.Value] = node;
        return Task.CompletedTask;
    }

    public Task<Node?> GetAsync(NodeId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id.Value, out Node? node) ? node : null);

    public Task UpdateAsync(Node node, CancellationToken cancellationToken = default)
    {
        _byId[node.Id.Value] = node;
        return Task.CompletedTask;
    }
}

internal sealed class FakeDeviceStore : IDeviceStore
{
    private readonly Dictionary<Guid, Device> _byId = [];

    public Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        _byId[device.Id.Value] = device;
        return Task.CompletedTask;
    }

    public Task<Device?> GetAsync(DeviceId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id.Value, out Device? device) ? device : null);

    public Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
    {
        _byId[device.Id.Value] = device;
        return Task.CompletedTask;
    }
}

internal sealed class FakeSnapshotStore : ISnapshotStore
{
    private readonly Dictionary<Guid, StoredSnapshot> _byId = [];

    public Task AddAsync(StoredSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _byId[snapshot.Metadata.Id.Value] = snapshot;
        return Task.CompletedTask;
    }

    public Task<StoredSnapshot?> GetAsync(SnapshotId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id.Value, out StoredSnapshot? s) ? s : null);

    public Task<IReadOnlyList<StoredSnapshot>> ListByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StoredSnapshot>>(
            _byId.Values.Where(s => s.Metadata.DeviceId == deviceId).ToArray());

    public Task<StoredSnapshot?> FindCompletedBySnapshotHashAsync(
        DeviceId deviceId,
        SnapshotHash snapshotHash,
        CancellationToken cancellationToken = default)
    {
        StoredSnapshot? match = _byId.Values.FirstOrDefault(s =>
            s.Metadata.DeviceId == deviceId
            && s.Metadata.Status == SnapshotStatus.Completed
            && s.Metadata.SnapshotHash is { } hash
            && hash.Equals(snapshotHash));
        return Task.FromResult(match);
    }
}

internal sealed class FakeConnectionProfileReadStore : IConnectionProfileReadStore
{
    public Dictionary<Guid, ConnectionProfileReadModel> ByDevice { get; } = [];

    public Task<ConnectionProfileReadModel?> GetAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(ByDevice.TryGetValue(deviceId.Value, out ConnectionProfileReadModel? m) ? m : null);
}

internal sealed class FakeRouterOsReadPort : IRouterOsReadPort
{
    public bool MutatedRouterOs { get; private set; }

    public Task<RouterOsProbeResult> ProbeAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        // Read-only probe — never mutates RouterOS.
        MutatedRouterOs = false;
        return Task.FromResult(new RouterOsProbeResult
        {
            Identity = $"CHR-{target.DeviceId.Value:N}"[..16],
            SupportState = SupportState.Supported,
        });
    }
}

internal sealed class FakeSnapshotCapturePort : ISnapshotCapturePort
{
    public SnapshotCaptureResult NextResult { get; set; } = CreateResult(Enumerable.Repeat((byte)1, 32).ToArray());

    public int CaptureCount { get; private set; }

    public Task<SnapshotCaptureResult> CaptureAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        CaptureCount++;
        return Task.FromResult(NextResult);
    }

    public static SnapshotCaptureResult CreateResult(byte[] digest32)
    {
        Hash256 digest = Hash256.Create(digest32);
        return new SnapshotCaptureResult
        {
            ConfigurationHash = ConfigurationHash.FromDigest(digest),
            ObservationHash = ObservationHash.FromDigest(digest),
            CapabilityHash = CapabilityHash.FromDigest(digest),
            SnapshotHash = SnapshotHash.FromDigest(digest),
            SchemaVersion = 1,
        };
    }
}

internal sealed class FakeStableReadCoordinatorPort : IStableReadCoordinatorPort
{
    public StableReadCoordinationResult NextResult { get; set; } = new()
    {
        Outcome = StableReadOutcomeCodes.Accepted,
        AttemptsUsed = 1,
        ConfigurationFingerprintHex = new string('a', 64),
        DiscoverySectionDigests = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["filter"] = new string('b', 64),
        },
    };

    public int CoordinateCount { get; private set; }

    public Task<StableReadCoordinationResult> CoordinateAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        CoordinateCount++;
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeConnectionProfileService : IConnectionProfileService
{
    public List<UpsertConnectionProfileCommand> Upserts { get; } = [];

    public Exception? ThrowOnUpsert { get; set; }

    public Task<ConnectionProfileView> UpsertAsync(
        UpsertConnectionProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnUpsert is not null)
        {
            throw ThrowOnUpsert;
        }

        Upserts.Add(command);
        return Task.FromResult(new ConnectionProfileView
        {
            DeviceId = command.DeviceId,
            Username = command.Username,
            SecretReference = Guid.NewGuid(),
            TrustMode = command.TrustMode,
            CaProfileRef = command.CaProfileRef,
            PinnedSpkiSha256Hex = command.PinnedSpkiSha256?.ToString(),
            ConnectTimeoutMs = command.ConnectTimeoutMs,
            CommandTimeoutMs = command.CommandTimeoutMs,
            MaxResponseBytes = command.MaxResponseBytes,
            RowVersion = 1,
        });
    }

    public Task<ConnectionProfileView> RotatePasswordAsync(
        Guid deviceId,
        ReadOnlyMemory<byte> newPasswordUtf8,
        string actor,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ConnectionProfileView> ChangeSpkiPinAsync(
        Guid deviceId,
        Hash256 newPin,
        string actor,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<ConnectionProfileView?> GetViewAsync(Guid deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult<ConnectionProfileView?>(null);
}
