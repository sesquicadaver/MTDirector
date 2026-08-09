using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Canonicalization;
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

    public Task<IReadOnlyList<Site>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Site>>(
            _byId.Values.OrderBy(s => s.Code.Value, StringComparer.Ordinal).ThenBy(s => s.Id.Value).ToArray());

    public Task<SitePage> ListPageAsync(int limit, string? cursor, CancellationToken cancellationToken = default)
    {
        int take = Math.Clamp(limit, 1, 200);
        List<Site> ordered = _byId.Values
            .OrderBy(s => s.Code.Value, StringComparer.Ordinal)
            .ThenBy(s => s.Id.Value)
            .ToList();
        int skip = 0;
        if (!string.IsNullOrWhiteSpace(cursor) && int.TryParse(cursor, out int parsed))
        {
            skip = Math.Max(0, parsed);
        }

        List<Site> page = ordered.Skip(skip).Take(take).ToList();
        string? next = skip + page.Count < ordered.Count
            ? (skip + page.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;
        return Task.FromResult(new SitePage { Items = page, NextCursor = next });
    }
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

    public Task<IReadOnlyList<Node>> ListBySiteAsync(SiteId siteId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Node>>(
            _byId.Values
                .Where(n => n.SiteId == siteId)
                .OrderBy(n => n.Name.Value, StringComparer.Ordinal)
                .ThenBy(n => n.Id.Value)
                .ToArray());
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

    public Task<IReadOnlyList<Device>> ListByNodeAsync(NodeId nodeId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Device>>(
            _byId.Values
                .Where(d => d.NodeId == nodeId)
                .OrderBy(d => d.DisplayName.Value, StringComparer.Ordinal)
                .ThenBy(d => d.Id.Value)
                .ToArray());
}

internal sealed class FakeIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<(string Actor, string Operation, Guid Key), (byte[] Hash, Guid ResourceId)> _records = [];

    public Task<IdempotencyLookupResult> TryGetAsync(
        string actor,
        string operation,
        Guid idempotencyKey,
        ReadOnlyMemory<byte> requestHash,
        CancellationToken cancellationToken = default)
    {
        if (!_records.TryGetValue((actor.Trim(), operation.Trim(), idempotencyKey), out var existing))
        {
            return Task.FromResult(new IdempotencyLookupResult { Found = false });
        }

        if (!existing.Hash.AsSpan().SequenceEqual(requestHash.Span))
        {
            return Task.FromResult(new IdempotencyLookupResult { Found = true, Conflict = true });
        }

        return Task.FromResult(new IdempotencyLookupResult
        {
            Found = true,
            ResourceId = existing.ResourceId,
        });
    }

    public Task SaveAsync(
        string actor,
        string operation,
        Guid idempotencyKey,
        ReadOnlyMemory<byte> requestHash,
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        _records[(actor.Trim(), operation.Trim(), idempotencyKey)] = (requestHash.ToArray(), resourceId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeAuditEventWriter : IAuditEventWriter
{
    public List<(string Actor, string Action, string PayloadJson)> Events { get; } = [];

    public Task AppendAsync(
        string actor,
        string action,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        Events.Add((actor, action, payloadJson));
        return Task.CompletedTask;
    }
}

internal sealed class FakeSnapshotStore : ISnapshotStore
{
    private readonly Dictionary<Guid, StoredSnapshot> _byId = [];
    private readonly Dictionary<(Guid RequestedBy, Guid Key), Guid> _idempotency = [];
    private readonly Dictionary<string, StoredSnapshotPayload> _payloads = new(StringComparer.Ordinal);

    /// <summary>Injected or persist-parsed canonical sections keyed by snapshot id (M1-24).</summary>
    public Dictionary<Guid, List<CanonicalSection>> SectionsBySnapshot { get; } = [];

    public Task<StoredSnapshot?> GetAsync(SnapshotId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id.Value, out StoredSnapshot? s) ? s : null);

    public Task<IReadOnlyList<StoredSnapshot>> ListByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StoredSnapshot>>(
            _byId.Values.Where(s => s.Metadata.DeviceId == deviceId).ToArray());

    public Task<StoredSnapshotPage> ListByDevicePageAsync(
        DeviceId deviceId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        List<StoredSnapshot> all = _byId.Values
            .Where(s => s.Metadata.DeviceId == deviceId)
            .OrderByDescending(s => s.Metadata.CompletedAtUtc)
            .ThenByDescending(s => s.Metadata.Id.Value)
            .ToList();
        int skip = 0;
        if (!string.IsNullOrWhiteSpace(cursor) && int.TryParse(cursor, out int parsed))
        {
            skip = Math.Max(0, parsed);
        }

        List<StoredSnapshot> page = all.Skip(skip).Take(limit).ToList();
        string? next = skip + page.Count < all.Count
            ? (skip + page.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;
        return Task.FromResult(new StoredSnapshotPage { Items = page, NextCursor = next });
    }

    public Task AddAsync(StoredSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _byId[snapshot.Metadata.Id.Value] = snapshot;
        return Task.CompletedTask;
    }

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

    public Task<StoredSnapshot?> FindByIdempotencyAsync(
        Guid requestedBy,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (_idempotency.TryGetValue((requestedBy, idempotencyKey), out Guid id)
            && _byId.TryGetValue(id, out StoredSnapshot? snapshot))
        {
            return Task.FromResult<StoredSnapshot?>(snapshot);
        }

        return Task.FromResult<StoredSnapshot?>(null);
    }

    public Task<StoredSnapshot> PersistCompletedAsync(
        SnapshotPersistRequest request,
        CancellationToken cancellationToken = default)
    {
        Hash256 rawHash = StorePayload(request.Capture.RawPayload, SnapshotPayloadKind.RawSanitized, request.Capture.SchemaVersion);
        Hash256 configPayloadHash = StorePayload(
            request.Capture.ConfigurationPayload,
            SnapshotPayloadKind.CanonicalConfiguration,
            request.Capture.SchemaVersion);
        Hash256 obsPayloadHash = StorePayload(
            request.Capture.ObservationPayload,
            SnapshotPayloadKind.CanonicalObservations,
            request.Capture.SchemaVersion);
        Hash256 capPayloadHash = StorePayload(
            request.Capture.CapabilityPayload,
            SnapshotPayloadKind.CanonicalCapabilities,
            request.Capture.SchemaVersion);

        SnapshotMetadata metadata = SnapshotMetadata.CreateCompleted(
            request.DeviceId,
            request.Capture.ConfigurationHash,
            request.Capture.ObservationHash,
            request.Capture.CapabilityHash,
            request.Capture.SnapshotHash,
            request.CapturedAtUtc);

        StoredSnapshot stored = new()
        {
            Metadata = metadata,
            SchemaVersion = request.Capture.SchemaVersion,
            OperationId = Guid.NewGuid(),
            RawPayloadHash = rawHash,
            ConfigurationPayloadHash = configPayloadHash,
            ObservationPayloadHash = obsPayloadHash,
            CapabilityPayloadHash = capPayloadHash,
        };
        _byId[stored.Metadata.Id.Value] = stored;
        _idempotency[(request.RequestedBy, request.IdempotencyKey)] = stored.Metadata.Id.Value;
        SectionsBySnapshot[stored.Metadata.Id.Value] = ParseSections(request.Capture);
        return Task.FromResult(stored);
    }

    public Task<StoredSnapshotPayload?> GetPayloadAsync(
        Hash256 payloadHash,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_payloads.TryGetValue(payloadHash.ToString(), out StoredSnapshotPayload? p) ? p : null);

    public Task<IReadOnlyList<CanonicalSection>> LoadCanonicalSectionsAsync(
        SnapshotId id,
        CancellationToken cancellationToken = default)
    {
        if (SectionsBySnapshot.TryGetValue(id.Value, out List<CanonicalSection>? sections))
        {
            return Task.FromResult<IReadOnlyList<CanonicalSection>>(sections);
        }

        return Task.FromResult<IReadOnlyList<CanonicalSection>>([]);
    }

    private static List<CanonicalSection> ParseSections(SnapshotCaptureResult capture)
    {
        List<CanonicalSection> sections = [];
        foreach (CapturedSectionDescriptor descriptor in capture.Sections)
        {
            if (descriptor.ConfigurationPayload is { Length: > 0 } config
                && CanonicalSection.TryParse(config.Span, out CanonicalSection? configSection)
                && configSection is not null)
            {
                sections.Add(configSection);
            }

            if (descriptor.ObservationPayload is { Length: > 0 } obs
                && CanonicalSection.TryParse(obs.Span, out CanonicalSection? obsSection)
                && obsSection is not null)
            {
                sections.Add(obsSection);
            }
        }

        return sections;
    }

    private Hash256 StorePayload(ReadOnlyMemory<byte> bytes, SnapshotPayloadKind kind, int schemaVersion)
    {
        if (bytes.Length == 0)
        {
            bytes = Encoding.UTF8.GetBytes("{}");
        }

        byte[] copy = bytes.ToArray();
        Hash256 hash = Hash256.Create(SHA256.HashData(copy));
        string key = hash.ToString();
        if (!_payloads.ContainsKey(key))
        {
            _payloads[key] = new StoredSnapshotPayload
            {
                PayloadHash = hash,
                Kind = kind,
                SchemaVersion = schemaVersion,
                Compression = SnapshotCompression.Brotli,
                UncompressedBytes = copy,
            };
        }

        return hash;
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

    public int ProbeCount { get; private set; }

    public TimeSpan ProbeDelay { get; set; }

    public Task<RouterOsProbeResult> ProbeAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ProbeCount++;
        MutatedRouterOs = false;
        if (ProbeDelay > TimeSpan.Zero)
        {
            return ProbeSlowAsync(target, cancellationToken);
        }

        return Task.FromResult(CreateResult(target));
    }

    private async Task<RouterOsProbeResult> ProbeSlowAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken)
    {
        await Task.Delay(ProbeDelay, cancellationToken).ConfigureAwait(false);
        return CreateResult(target);
    }

    private static RouterOsProbeResult CreateResult(RouterOsReadTarget target)
        => new()
        {
            Identity = $"CHR-{target.DeviceId.Value:N}"[..16],
            SupportState = SupportState.Supported,
        };
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
        byte[] body = Encoding.UTF8.GetBytes(
            "{\"digest\":\"" + Convert.ToHexString(digest32).ToLowerInvariant() + "\"}");
        return new SnapshotCaptureResult
        {
            ConfigurationHash = ConfigurationHash.FromDigest(digest),
            ObservationHash = ObservationHash.FromDigest(digest),
            CapabilityHash = CapabilityHash.FromDigest(digest),
            SnapshotHash = SnapshotHash.FromDigest(digest),
            SchemaVersion = 1,
            RawPayload = body,
            ConfigurationPayload = body,
            ObservationPayload = body,
            CapabilityPayload = body,
            Sections =
            [
                new CapturedSectionDescriptor
                {
                    SectionId = "system.identity",
                    SectionVersion = 1,
                    Status = 1,
                    Ordered = false,
                    ConfigurationRecordCount = 1,
                    ConfigurationPayload = body,
                },
            ],
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

    public Dictionary<Guid, ConnectionProfileView> Views { get; } = [];

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
        ConnectionProfileView view = new()
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
        };
        Views[command.DeviceId] = view;
        return Task.FromResult(view);
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
        => Task.FromResult(Views.TryGetValue(deviceId, out ConnectionProfileView? view) ? view : null);
}
