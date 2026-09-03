using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Integration;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Incident;
using Mfc.Domain;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Drift;
using Mfc.Domain.Drift.Primitives;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Incident.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Routing;
using Mfc.Domain.Snapshots;
using Mfc.Domain.Workflow;

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

internal sealed class FakeOnboardingStore : IOnboardingStore
{
    private readonly Dictionary<Guid, OnboardingPlan> _plans = [];
    private readonly Dictionary<Guid, OnboardingOperation> _operations = [];
    private readonly Dictionary<Guid, OnboardingStep> _steps = [];

    public IReadOnlyCollection<OnboardingOperation> Operations => _operations.Values;

    public Task AddPlanAsync(OnboardingPlan plan, CancellationToken cancellationToken = default)
    {
        _plans[plan.Id.Value] = plan;
        return Task.CompletedTask;
    }

    public Task<OnboardingPlan?> GetPlanAsync(OnboardingPlanId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_plans.TryGetValue(id.Value, out OnboardingPlan? plan) ? plan : null);

    public Task AddOperationAsync(OnboardingOperation operation, CancellationToken cancellationToken = default)
    {
        _operations[operation.Id.Value] = operation;
        return Task.CompletedTask;
    }

    public Task SaveOperationAsync(OnboardingOperation operation, CancellationToken cancellationToken = default)
    {
        _operations[operation.Id.Value] = operation;
        return Task.CompletedTask;
    }

    public Task<OnboardingOperation?> GetOperationAsync(
        OnboardingOperationId id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_operations.TryGetValue(id.Value, out OnboardingOperation? operation) ? operation : null);

    public Task<IReadOnlyList<OnboardingOperation>> ListNonterminalByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OnboardingOperation>>(
            _operations.Values.Where(o => o.NodeId == nodeId && o.IsNonterminal).ToArray());

    public Task<IReadOnlyList<OnboardingOperation>> ListNonterminalAsync(
        int limit,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OnboardingOperation>>(
            _operations.Values
                .Where(static o => o.IsNonterminal)
                .OrderBy(static o => o.CreatedAtUtc)
                .Take(Math.Max(0, limit))
                .ToArray());

    public Task AddStepAsync(OnboardingStep onboardingStep, CancellationToken cancellationToken = default)
    {
        _steps[onboardingStep.Id.Value] = onboardingStep;
        return Task.CompletedTask;
    }

    public Task SaveStepAsync(OnboardingStep onboardingStep, CancellationToken cancellationToken = default)
    {
        _steps[onboardingStep.Id.Value] = onboardingStep;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OnboardingStep>> ListStepsAsync(
        OnboardingOperationId operationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OnboardingStep>>(
            _steps.Values.Where(s => s.OperationId == operationId).ToArray());
}

internal sealed class FakeDeploymentStore : IDeploymentStore
{
    private readonly Dictionary<Guid, DeploymentPlan> _plans = [];
    private readonly Dictionary<Guid, DeploymentOperation> _operations = [];
    private readonly Dictionary<(Guid OperationId, Guid DeviceId), DeviceDeployment> _devices = [];
    private readonly Dictionary<Guid, DeploymentStep> _steps = [];
    private readonly Dictionary<Guid, DeploymentLock> _locks = [];

    public IReadOnlyCollection<DeploymentOperation> Operations => _operations.Values;

    public Task AddPlanAsync(DeploymentPlan plan, CancellationToken cancellationToken = default)
    {
        _plans[plan.Id.Value] = plan;
        return Task.CompletedTask;
    }

    public Task<DeploymentPlan?> GetPlanAsync(DeploymentPlanId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_plans.TryGetValue(id.Value, out DeploymentPlan? plan) ? plan : null);

    public Task AddOperationAsync(DeploymentOperation operation, CancellationToken cancellationToken = default)
    {
        _operations[operation.Id.Value] = operation;
        return Task.CompletedTask;
    }

    public Task SaveOperationAsync(DeploymentOperation operation, CancellationToken cancellationToken = default)
    {
        _operations[operation.Id.Value] = operation;
        return Task.CompletedTask;
    }

    public Task<DeploymentOperation?> GetOperationAsync(
        DeploymentOperationId id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_operations.TryGetValue(id.Value, out DeploymentOperation? operation) ? operation : null);

    public Task<IReadOnlyList<DeploymentOperation>> ListNonterminalByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DeploymentOperation>>(
            _operations.Values.Where(o => o.NodeId == nodeId && o.IsNonterminal).ToArray());

    public Task<IReadOnlyList<DeploymentOperation>> ListNonterminalAsync(
        int limit,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DeploymentOperation>>(
            _operations.Values.Where(static o => o.IsNonterminal)
                .OrderBy(static o => o.CreatedAtUtc)
                .Take(Math.Max(0, limit))
                .ToArray());

    public Task AddDeviceStateAsync(DeviceDeployment device, CancellationToken cancellationToken = default)
    {
        _devices[(device.OperationId.Value, device.DeviceId.Value)] = device;
        return Task.CompletedTask;
    }

    public Task SaveDeviceStateAsync(DeviceDeployment device, CancellationToken cancellationToken = default)
    {
        _devices[(device.OperationId.Value, device.DeviceId.Value)] = device;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeviceDeployment>> ListDeviceStatesAsync(
        DeploymentOperationId operationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DeviceDeployment>>(
            _devices.Values.Where(d => d.OperationId == operationId).ToArray());

    public Task AddStepAsync(DeploymentStep deploymentStep, CancellationToken cancellationToken = default)
    {
        _steps[deploymentStep.Id.Value] = deploymentStep;
        return Task.CompletedTask;
    }

    public Task SaveStepAsync(DeploymentStep deploymentStep, CancellationToken cancellationToken = default)
    {
        _steps[deploymentStep.Id.Value] = deploymentStep;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeploymentStep>> ListStepsAsync(
        DeploymentOperationId operationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DeploymentStep>>(
            _steps.Values.Where(s => s.OperationId == operationId).ToArray());

    public Task AddLockAsync(DeploymentLock deploymentLock, CancellationToken cancellationToken = default)
    {
        _locks[deploymentLock.NodeId.Value] = deploymentLock;
        return Task.CompletedTask;
    }

    public Task SaveLockAsync(DeploymentLock deploymentLock, CancellationToken cancellationToken = default)
    {
        _locks[deploymentLock.NodeId.Value] = deploymentLock;
        return Task.CompletedTask;
    }

    public Task<DeploymentLock?> GetLockByNodeAsync(NodeId nodeId, CancellationToken cancellationToken = default)
        => Task.FromResult(_locks.TryGetValue(nodeId.Value, out DeploymentLock? value) ? value : null);

    public Task<IReadOnlyList<DeploymentLock>> ListLocksByOwnerAsync(
        string ownerInstanceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DeploymentLock>>(
            _locks.Values
                .Where(l => string.Equals(l.OwnerInstanceId, ownerInstanceId, StringComparison.Ordinal))
                .OrderBy(l => l.NodeId.Value)
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

    /// <summary>Persisted section descriptors for GetSnapshotSummary (M1-28).</summary>
    public Dictionary<Guid, List<StoredSnapshotSectionDescriptor>> SectionDescriptorsBySnapshot { get; } = [];

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
        SectionDescriptorsBySnapshot[stored.Metadata.Id.Value] = request.Capture.Sections
            .Select(static s => new StoredSnapshotSectionDescriptor
            {
                SectionId = s.SectionId,
                Status = s.Status,
                Ordered = s.Ordered,
                ConfigurationRecordCount = s.ConfigurationRecordCount,
                ObservationRecordCount = s.ObservationRecordCount,
            })
            .ToList();
        return Task.FromResult(stored);
    }

    public Task<StoredSnapshotPayload?> GetPayloadAsync(
        Hash256 payloadHash,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_payloads.TryGetValue(payloadHash.ToString(), out StoredSnapshotPayload? p) ? p : null);

    /// <summary>Registers a content-addressed payload for unit tests.</summary>
    public Hash256 RegisterPayload(ReadOnlyMemory<byte> bytes, SnapshotPayloadKind kind = SnapshotPayloadKind.RawSanitized)
    {
        byte[] copy = bytes.ToArray();
        Hash256 hash = Hash256.Create(SHA256.HashData(copy));
        _payloads[hash.ToString()] = new StoredSnapshotPayload
        {
            PayloadHash = hash,
            Kind = kind,
            SchemaVersion = 1,
            Compression = SnapshotCompression.Brotli,
            UncompressedBytes = copy,
        };
        return hash;
    }

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

    public Task<IReadOnlyList<StoredSnapshotSectionDescriptor>> ListSectionDescriptorsAsync(
        SnapshotId id,
        CancellationToken cancellationToken = default)
    {
        if (SectionDescriptorsBySnapshot.TryGetValue(id.Value, out List<StoredSnapshotSectionDescriptor>? rows))
        {
            return Task.FromResult<IReadOnlyList<StoredSnapshotSectionDescriptor>>(rows);
        }

        return Task.FromResult<IReadOnlyList<StoredSnapshotSectionDescriptor>>([]);
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

    public int NeighborListCount { get; private set; }

    public TimeSpan ProbeDelay { get; set; }

    public Exception? ThrowOnProbe { get; set; }

    public Exception? ThrowOnNeighborList { get; set; }

    public RouterOsNeighborDiscoveryResult NeighborResult { get; set; } = new()
    {
        SeedIdentity = "seed-chr",
        Rows = [],
    };

    public Task<RouterOsProbeResult> ProbeAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ProbeCount++;
        MutatedRouterOs = false;
        if (ThrowOnProbe is not null)
        {
            throw ThrowOnProbe;
        }
        if (ProbeDelay > TimeSpan.Zero)
        {
            return ProbeSlowAsync(target, cancellationToken);
        }

        return Task.FromResult(CreateResult(target));
    }

    public Task<RouterOsNeighborDiscoveryResult> ListNeighborRowsAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        NeighborListCount++;
        MutatedRouterOs = false;
        if (ThrowOnNeighborList is not null)
        {
            throw ThrowOnNeighborList;
        }

        return Task.FromResult(NeighborResult);
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

internal sealed class FakeZoneDefinitionStore : IZoneDefinitionStore
{
    private readonly Dictionary<Guid, ZoneDefinition> _byId = [];

    public Task AddAsync(ZoneDefinition zone, CancellationToken cancellationToken = default)
    {
        _byId[zone.Id.Value] = zone;
        return Task.CompletedTask;
    }

    public Task<ZoneDefinition?> GetAsync(ZoneId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id.Value, out ZoneDefinition? zone) ? zone : null);

    public Task UpdateAsync(ZoneDefinition zone, CancellationToken cancellationToken = default)
    {
        _byId[zone.Id.Value] = zone;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ZoneId id, CancellationToken cancellationToken = default)
    {
        _byId.Remove(id.Value);
        return Task.CompletedTask;
    }

    public Task<bool> KeyExistsAsync(
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        NonEmptyName key,
        ZoneId? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        bool exists = _byId.Values.Any(z =>
            z.OwnerScope == ownerScope
            && z.OwnerId == ownerId
            && z.Key.Equals(key)
            && (excludingId is null || z.Id.Value != excludingId.Value.Value));
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<ZoneDefinition>> ListAsync(
        PolicyOwnerScope? ownerScope = null,
        Guid? ownerId = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<ZoneDefinition> query = _byId.Values;
        if (ownerScope is not null)
        {
            query = query.Where(z => z.OwnerScope == ownerScope && z.OwnerId == ownerId);
        }

        return Task.FromResult<IReadOnlyList<ZoneDefinition>>(
            query.OrderBy(z => z.Key.Value, StringComparer.Ordinal).ToArray());
    }
}

internal sealed class FakeNodeZoneBindingStore : INodeZoneBindingStore
{
    private readonly Dictionary<Guid, NodeZoneBinding> _byId = [];

    public Task AddAsync(NodeZoneBinding binding, CancellationToken cancellationToken = default)
    {
        _byId[binding.Id.Value] = Clone(binding);
        return Task.CompletedTask;
    }

    public Task<NodeZoneBinding?> GetAsync(NodeZoneBindingId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id.Value, out NodeZoneBinding? b) ? Clone(b) : null);

    public Task<NodeZoneBinding?> GetByNodeAndZoneAsync(
        NodeId nodeId,
        ZoneId zoneId,
        CancellationToken cancellationToken = default)
    {
        NodeZoneBinding? found = _byId.Values.FirstOrDefault(b => b.NodeId == nodeId && b.ZoneId == zoneId);
        return Task.FromResult(found is null ? null : Clone(found));
    }

    public Task UpdateAsync(NodeZoneBinding binding, CancellationToken cancellationToken = default)
    {
        _byId[binding.Id.Value] = Clone(binding);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(NodeZoneBindingId id, CancellationToken cancellationToken = default)
    {
        _byId.Remove(id.Value);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NodeZoneBinding>> ListByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NodeZoneBinding>>(
            _byId.Values.Where(b => b.NodeId == nodeId).OrderBy(b => b.Id.Value).Select(Clone).ToArray());

    public Task<IReadOnlyList<NodeZoneBinding>> ListByZoneAsync(
        ZoneId zoneId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NodeZoneBinding>>(
            _byId.Values.Where(b => b.ZoneId == zoneId).OrderBy(b => b.Id.Value).Select(Clone).ToArray());

    public Task<int> CountByZoneAsync(ZoneId zoneId, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.Values.Count(b => b.ZoneId == zoneId));

    private static NodeZoneBinding Clone(NodeZoneBinding binding)
        => NodeZoneBinding.Reconstitute(
            binding.Id,
            binding.NodeId,
            binding.ZoneId,
            binding.Kind,
            binding.Values.ToArray(),
            binding.ExpectedDependencyHash,
            binding.LastResolvedDependencyHash,
            binding.AnalysisStale,
            binding.RowVersion);
}

internal sealed class FakeZoneResolveObservationSource : IZoneResolveObservationSource
{
    public Dictionary<Guid, ZoneResolveDeviceObservation> ByDevice { get; } = [];

    public Task<ZoneResolveDeviceObservation> GetForDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        if (ByDevice.TryGetValue(deviceId.Value, out ZoneResolveDeviceObservation? observation))
        {
            return Task.FromResult(observation);
        }

        return Task.FromResult(new ZoneResolveDeviceObservation
        {
            DeviceId = deviceId,
            Interfaces = [],
            InterfaceLists = [],
            InterfaceListMembers = [],
            ObservationAvailable = false,
        });
    }
}

internal sealed class FakePolicyStore : IPolicyStore
{
    private readonly Dictionary<Guid, Domain.Policy.Policy> _policies = [];
    private readonly Dictionary<Guid, PolicyRevision> _revisions = [];

    public Task AddPolicyAsync(Domain.Policy.Policy policy, CancellationToken cancellationToken = default)
    {
        _policies[policy.Id.Value] = policy;
        return Task.CompletedTask;
    }

    public Task<Domain.Policy.Policy?> GetPolicyAsync(PolicyId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_policies.TryGetValue(id.Value, out Domain.Policy.Policy? policy) ? policy : null);

    public Task UpdatePolicyAsync(Domain.Policy.Policy policy, CancellationToken cancellationToken = default)
    {
        _policies[policy.Id.Value] = policy;
        return Task.CompletedTask;
    }

    public Task AddRevisionAsync(PolicyRevision revision, CancellationToken cancellationToken = default)
    {
        _revisions[revision.Id.Value] = Clone(revision);
        return Task.CompletedTask;
    }

    public Task SaveRevisionAsync(PolicyRevision revision, CancellationToken cancellationToken = default)
    {
        _revisions[revision.Id.Value] = Clone(revision);
        return Task.CompletedTask;
    }

    /// <summary>Test helper: drops a revision so compile can exercise missing-revision paths.</summary>
    public void RemoveRevision(PolicyRevisionId id) => _revisions.Remove(id.Value);

    /// <summary>Test helper: drops a policy container for compose fail-path coverage.</summary>
    public void RemovePolicy(PolicyId id) => _policies.Remove(id.Value);

    public Task<PolicyRevision?> GetRevisionAsync(PolicyRevisionId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_revisions.TryGetValue(id.Value, out PolicyRevision? revision) ? Clone(revision) : null);

    public Task<IReadOnlyList<PolicyRevision>> ListRevisionsAsync(
        PolicyId policyId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PolicyRevision>>(
            _revisions.Values
                .Where(r => r.PolicyId == policyId)
                .OrderBy(r => r.RevisionNumber)
                .Select(Clone)
                .ToArray());

    public Task<uint> GetLatestRevisionNumberAsync(PolicyId policyId, CancellationToken cancellationToken = default)
    {
        uint max = _revisions.Values
            .Where(r => r.PolicyId == policyId)
            .Select(r => r.RevisionNumber)
            .DefaultIfEmpty(0u)
            .Max();
        return Task.FromResult(max);
    }

    public Task<IReadOnlyList<Domain.Policy.Policy>> ListActiveByKindAsync(
        PolicyKind kind,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Domain.Policy.Policy>>(
            _policies.Values
                .Where(p => p.Kind == kind && p.Status == PolicyStatus.Active)
                .OrderBy(p => p.Id.Value)
                .ToArray());

    public Task<IReadOnlyList<Domain.Policy.Policy>> ListActiveByOwnerAsync(
        PolicyKind kind,
        Guid ownerId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Domain.Policy.Policy>>(
            _policies.Values
                .Where(p => p.Kind == kind && p.Status == PolicyStatus.Active && p.OwnerId == ownerId)
                .OrderBy(p => p.Id.Value)
                .ToArray());

    private static PolicyRevision Clone(PolicyRevision revision)
        => PolicyRevision.Reconstitute(
            revision.Id,
            revision.PolicyId,
            revision.RevisionNumber,
            revision.SchemaVersion,
            revision.ContentHash,
            revision.ParentContextHash,
            revision.State,
            revision.CreatedBy,
            revision.CreatedAtUtc,
            revision.ApprovedAtUtc,
            revision.CanonicalBytes.ToArray(),
            revision.ApprovedAnalysisRunId,
            revision.ApprovedBundleHash);
}

internal sealed class FakeClock : Mfc.Application.Abstractions.Time.IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int ExecuteCount { get; private set; }

    public List<string> NestedSteps { get; } = [];

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteCount++;
        return action(cancellationToken);
    }
}

internal sealed class FakePolicyApprovalStore : IPolicyApprovalStore
{
    private readonly Dictionary<Guid, PolicyAnalysisRun> _runs = [];
    private readonly Dictionary<Guid, PolicyWarningAcknowledgment> _acks = [];
    private readonly Dictionary<Guid, PolicyApproval> _votes = [];
    private readonly Dictionary<Guid, PolicyDesiredBinding> _bindings = [];

    public Task AddAnalysisRunAsync(PolicyAnalysisRun run, CancellationToken cancellationToken = default)
    {
        _runs[run.Id.Value] = CloneRun(run);
        return Task.CompletedTask;
    }

    public Task<PolicyAnalysisRun?> GetAnalysisRunAsync(
        PolicyAnalysisRunId id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_runs.TryGetValue(id.Value, out PolicyAnalysisRun? run) ? CloneRun(run) : null);

    public Task<IReadOnlyList<PolicyAnalysisRun>> ListAnalysisRunsForRevisionAsync(
        PolicyRevisionId revisionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PolicyAnalysisRun>>(
            _runs.Values.Where(r => r.RevisionId == revisionId).Select(CloneRun).ToArray());

    public Task AddWarningAcknowledgmentAsync(
        PolicyWarningAcknowledgment acknowledgment,
        CancellationToken cancellationToken = default)
    {
        _acks[acknowledgment.Id.Value] = acknowledgment;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PolicyWarningAcknowledgment>> ListAcknowledgmentsAsync(
        PolicyAnalysisRunId analysisRunId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PolicyWarningAcknowledgment>>(
            _acks.Values.Where(a => a.AnalysisRunId == analysisRunId).ToArray());

    public Task AddApprovalAsync(PolicyApproval approval, CancellationToken cancellationToken = default)
    {
        _votes[approval.Id.Value] = approval;
        return Task.CompletedTask;
    }

    public Task<PolicyApproval?> GetApprovalAsync(
        PolicyApprovalId id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_votes.TryGetValue(id.Value, out PolicyApproval? vote) ? vote : null);

    public Task<IReadOnlyList<PolicyApproval>> ListApprovalsAsync(
        PolicyRevisionId revisionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PolicyApproval>>(
            _votes.Values.Where(v => v.RevisionId == revisionId).ToArray());

    public Task AddBindingAsync(PolicyDesiredBinding binding, CancellationToken cancellationToken = default)
    {
        _bindings[binding.Id.Value] = CloneBinding(binding);
        return Task.CompletedTask;
    }

    public Task SaveBindingAsync(PolicyDesiredBinding binding, CancellationToken cancellationToken = default)
    {
        _bindings[binding.Id.Value] = CloneBinding(binding);
        return Task.CompletedTask;
    }

    public Task<PolicyDesiredBinding?> GetBindingAsync(
        PolicyBindingId id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_bindings.TryGetValue(id.Value, out PolicyDesiredBinding? binding) ? CloneBinding(binding) : null);

    public Task<IReadOnlyList<PolicyDesiredBinding>> ListActiveBindingsAsync(
        PolicyBindingScope scope,
        Guid? scopeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PolicyDesiredBinding>>(
            _bindings.Values
                .Where(b => b.Scope == scope && b.ScopeId == scopeId)
                .Select(CloneBinding)
                .ToArray());

    public Task<IReadOnlyList<PolicyDesiredBinding>> ListDueExceptionBindingsAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = nowUtc.ToUniversalTime();
        return Task.FromResult<IReadOnlyList<PolicyDesiredBinding>>(
            _bindings.Values
                .Where(b => b.Scope == PolicyBindingScope.Exception
                            && b.State == PolicyBindingState.Active
                            && b.ValidUntilUtc is not null
                            && now >= b.ValidUntilUtc.Value)
                .OrderBy(b => b.ValidUntilUtc)
                .ThenBy(b => b.Id.Value)
                .Take(Math.Max(0, limit))
                .Select(CloneBinding)
                .ToArray());
    }

    public Task<IReadOnlyList<PolicyDesiredBinding>> ListDueIncidentDenyOverlayBindingsAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = nowUtc.ToUniversalTime();
        return Task.FromResult<IReadOnlyList<PolicyDesiredBinding>>(
            _bindings.Values
                .Where(b => b.Scope == PolicyBindingScope.IncidentDenyOverlay
                            && b.State == PolicyBindingState.Active
                            && b.ValidUntilUtc is not null
                            && now >= b.ValidUntilUtc.Value)
                .OrderBy(b => b.ValidUntilUtc)
                .ThenBy(b => b.Id.Value)
                .Take(Math.Max(0, limit))
                .Select(CloneBinding)
                .ToArray());
    }

    private static PolicyAnalysisRun CloneRun(PolicyAnalysisRun run)
        => PolicyAnalysisRun.Reconstitute(
            run.Id,
            run.RevisionId,
            run.RevisionContentHash,
            run.LogicalEffectiveHash,
            run.AnalysisContextHash,
            run.EvidenceContextHash,
            run.TopologyProjectionHash,
            run.ImpactSetHash,
            run.PerDeviceAnalysisHashes,
            run.BundleHash,
            run.DependencyFingerprint,
            run.RiskLevel,
            run.EvidenceSignalsPresent,
            run.AnalyzerVersion,
            run.PolicySchemaVersion,
            run.PipelineVersion,
            run.Findings,
            run.TestResults,
            run.CreatedBy,
            run.CreatedAtUtc);

    private static PolicyDesiredBinding CloneBinding(PolicyDesiredBinding binding)
        => PolicyDesiredBinding.Reconstitute(
            binding.Id,
            binding.Scope,
            binding.ScopeId,
            binding.PolicyId,
            binding.DesiredRevisionId,
            binding.AnalysisRunId,
            binding.BundleHash,
            binding.State,
            binding.ValidFromUtc,
            binding.ValidUntilUtc,
            binding.RowVersion,
            binding.CreatedAtUtc,
            binding.UpdatedAtUtc);
}

internal sealed class FakeFilterArtifactStore : IFilterArtifactStore
{
    public Dictionary<string, StoredFilterArtifact> ByHash { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, byte[]> CanonicalBytesByHash { get; } = new(StringComparer.Ordinal);
    public List<(RouterOsFilterArtifact Artifact, CompilationProvenance Provenance)> Puts { get; } = [];

    public Task<StoredFilterArtifact?> GetByResourceHashAsync(
        Hash256 resourceHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceHash);
        return Task.FromResult(
            ByHash.TryGetValue(resourceHash.ToString(), out StoredFilterArtifact? stored) ? stored : null);
    }

    public Task<byte[]?> GetCanonicalBytesByResourceHashAsync(
        Hash256 resourceHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceHash);
        if (CanonicalBytesByHash.TryGetValue(resourceHash.ToString(), out byte[]? bytes))
        {
            return Task.FromResult<byte[]?>(bytes);
        }

        return Task.FromResult<byte[]?>(null);
    }

    public Task<StoredFilterArtifact> PutIfAbsentAsync(
        RouterOsFilterArtifact artifact,
        CompilationProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(provenance);
        Puts.Add((artifact, provenance));
        string key = artifact.ResourceHash.ToString();
        CanonicalBytesByHash[key] = artifact.CanonicalBytes.ToArray();
        if (ByHash.TryGetValue(key, out StoredFilterArtifact? existing))
        {
            return Task.FromResult(new StoredFilterArtifact
            {
                ResourceHash = existing.ResourceHash,
                ArtifactId = existing.ArtifactId,
                DeviceId = existing.DeviceId,
                PhysicalSemanticsHash = existing.PhysicalSemanticsHash,
                CompilerProfileHash = existing.CompilerProfileHash,
                LogicalEffectivePolicyHash = existing.LogicalEffectivePolicyHash,
                DeviceResolvedPolicyHash = existing.DeviceResolvedPolicyHash,
                AnalysisBundleHash = existing.AnalysisBundleHash,
                CapabilityHash = existing.CapabilityHash,
                CompilerVersion = existing.CompilerVersion,
                CompiledAtUtc = existing.CompiledAtUtc,
                CreatedAtUtc = existing.CreatedAtUtc,
                UncompressedSize = existing.UncompressedSize,
                Inserted = false,
            });
        }

        StoredFilterArtifact stored = new()
        {
            ResourceHash = artifact.ResourceHash,
            ArtifactId = artifact.ArtifactId,
            DeviceId = artifact.DeviceId,
            PhysicalSemanticsHash = artifact.PhysicalSemanticsHash,
            CompilerProfileHash = artifact.CompilerProfileHash,
            LogicalEffectivePolicyHash = provenance.LogicalEffectivePolicyHash,
            DeviceResolvedPolicyHash = provenance.DeviceResolvedPolicyHash,
            AnalysisBundleHash = provenance.AnalysisBundleHash,
            CapabilityHash = provenance.CapabilityHash,
            CompilerVersion = provenance.CompilerVersion,
            CompiledAtUtc = provenance.CompiledAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UncompressedSize = artifact.CanonicalBytes.Length,
            Inserted = true,
        };
        ByHash[key] = stored;
        return Task.FromResult(stored);
    }
}

internal sealed class FakeDeviceHashStateStore : IDeviceHashStateStore
{
    private readonly Dictionary<Guid, DeviceHashState> _byDevice = [];

    public Task UpsertAsync(DeviceHashState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        _byDevice[state.DeviceId.Value] = state;
        return Task.CompletedTask;
    }

    public Task<DeviceHashState?> GetAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(_byDevice.TryGetValue(deviceId.Value, out DeviceHashState? state) ? state : null);

    public Task<IReadOnlyList<DeviceHashState>> ListByDeviceIdsAsync(
        IReadOnlyList<DeviceId> deviceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);
        List<DeviceHashState> rows = [];
        foreach (DeviceId id in deviceIds)
        {
            if (_byDevice.TryGetValue(id.Value, out DeviceHashState? state))
            {
                rows.Add(state);
            }
        }

        return Task.FromResult<IReadOnlyList<DeviceHashState>>(
            rows.OrderBy(static s => s.DeviceId.Value).ToArray());
    }

    public Task<IReadOnlyList<DeviceHashState>> ListWithLastCommittedAsync(
        int limit,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DeviceHashState>>(
            _byDevice.Values
                .Where(static s => s.LastCommittedArtifactHash is not null)
                .OrderBy(static s => s.DeviceId.Value)
                .Take(Math.Max(0, limit))
                .ToArray());
}

internal sealed class FakeRoutingAssuranceStateStore : IRoutingAssuranceStateStore
{
    private readonly Dictionary<Guid, RoutingAssuranceState> _byDevice = [];

    public Task UpsertAsync(RoutingAssuranceState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        _byDevice[state.DeviceId.Value] = state;
        return Task.CompletedTask;
    }

    public Task<RoutingAssuranceState?> GetAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(_byDevice.TryGetValue(deviceId.Value, out RoutingAssuranceState? state) ? state : null);

    public void Seed(RoutingAssuranceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _byDevice[state.DeviceId.Value] = state;
    }
}

internal sealed class FakeDriftEventStore : IDriftEventStore
{
    private readonly Dictionary<Guid, DriftEvent> _byId = [];

    public Task AppendAsync(DriftEvent driftEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(driftEvent);
        _byId[driftEvent.Id.Value] = driftEvent;
        return Task.CompletedTask;
    }

    public Task<DriftEvent?> GetAsync(DriftEventId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id.Value, out DriftEvent? e) ? e : null);

    public Task<IReadOnlyList<DriftEvent>> ListByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DriftEvent>>(
            _byId.Values
                .Where(e => e.DeviceId == deviceId)
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id.Value)
                .ToArray());

    public Task<IReadOnlyList<DriftEvent>> ListByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DriftEvent>>(
            _byId.Values
                .Where(e => e.NodeId == nodeId)
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id.Value)
                .ToArray());

    public Task<bool> HasBlockingCriticalDriftAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<IGrouping<Guid, DriftEvent>> byDevice = _byId.Values
            .Where(e => e.NodeId == nodeId)
            .GroupBy(e => e.DeviceId.Value);
        foreach (IGrouping<Guid, DriftEvent> group in byDevice)
        {
            DriftEvent latest = group
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id.Value)
                .First();
            if (latest.BlocksDeployment)
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }
}

internal sealed class FakeResponseFeedbackEventStore : IResponseFeedbackEventStore
{
    private readonly Dictionary<Guid, ResponseFeedbackEvent> _byId = [];

    public Task AppendAsync(ResponseFeedbackEvent feedbackEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedbackEvent);
        _byId[feedbackEvent.Id.Value] = feedbackEvent;
        return Task.CompletedTask;
    }

    public Task<ResponseFeedbackEvent?> GetAsync(
        ResponseFeedbackEventId id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id.Value, out ResponseFeedbackEvent? e) ? e : null);

    public Task<IReadOnlyList<ResponseFeedbackEvent>> ListByIncidentAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ResponseFeedbackEvent>>(
            _byId.Values
                .Where(e => e.IncidentId == incidentId)
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id.Value)
                .ToArray());

    public Task<IReadOnlyList<ResponseFeedbackEvent>> ListByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ResponseFeedbackEvent>>(
            _byId.Values
                .Where(e => e.NodeId == nodeId)
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id.Value)
                .ToArray());
}

internal sealed class RecordingResponseFeedbackDeliveryPort : IResponseFeedbackDeliveryPort
{
    public List<ResponseFeedbackEvent> Delivered { get; } = [];

    public Task<ResponseFeedbackDeliveryResult> DeliverAsync(
        ResponseFeedbackEvent feedbackEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedbackEvent);
        Delivered.Add(feedbackEvent);
        return Task.FromResult(new ResponseFeedbackDeliveryResult
        {
            Outcome = ResponseFeedbackDeliveryOutcome.Delivered,
        });
    }
}

internal static class ResponseFeedbackTestFactory
{
    internal static EmitResponseFeedbackUseCase CreateEmit(
        FakeAuthorizationBoundary auth,
        FakeResponseFeedbackEventStore store,
        FakeAuditEventWriter audit,
        FakeClock clock,
        RecordingResponseFeedbackDeliveryPort? delivery = null)
        => new(
            auth,
            store,
            delivery ?? new RecordingResponseFeedbackDeliveryPort(),
            audit,
            clock,
            new FakeUnitOfWork());
}

internal sealed class FakeEndpointPresenceStore : IEndpointPresenceStore
{
    private readonly Dictionary<Guid, EndpointPresenceInterval> _intervals = [];
    private readonly Dictionary<Guid, EndpointRoutingContext> _contexts = [];

    public Task<EndpointPresenceInterval?> GetActiveIntervalAsync(
        EndpointId endpointId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_intervals.Values.SingleOrDefault(i =>
            i.EndpointId.Equals(endpointId) && i.IsActive));

    public Task<EndpointPresenceInterval?> GetIntervalAsOfAsync(
        EndpointId endpointId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_intervals.Values
            .Where(i => i.EndpointId.Equals(endpointId))
            .OrderByDescending(i => i.ValidFrom)
            .FirstOrDefault(i => i.Contains(asOfUtc)));

    public Task<EndpointRoutingContext?> GetRoutingContextAsync(
        PresenceId presenceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_contexts.TryGetValue(presenceId.Value, out EndpointRoutingContext? context) ? context : null);

    public Task<EndpointRoutingContext?> GetRoutingContextAsOfAsync(
        EndpointId endpointId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        EndpointPresenceInterval? interval = _intervals.Values
            .Where(i => i.EndpointId.Equals(endpointId))
            .OrderByDescending(i => i.ValidFrom)
            .FirstOrDefault(i => i.Contains(asOfUtc));
        return interval is null
            ? Task.FromResult<EndpointRoutingContext?>(null)
            : GetRoutingContextAsync(interval.PresenceId, cancellationToken);
    }

    public Task SaveMigrationAsync(
        EndpointPresenceInterval? closedInterval,
        EndpointPresenceInterval openedInterval,
        EndpointRoutingContext routingContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openedInterval);
        ArgumentNullException.ThrowIfNull(routingContext);

        if (closedInterval is not null)
        {
            _intervals[closedInterval.PresenceId.Value] = closedInterval;
            if (_contexts.TryGetValue(closedInterval.PresenceId.Value, out EndpointRoutingContext? closedContext))
            {
                _contexts[closedInterval.PresenceId.Value] = EndpointRoutingContext.Reconstitute(
                    closedContext.EndpointId,
                    closedContext.PresenceId,
                    closedContext.SiteId,
                    closedContext.NodeId,
                    closedContext.SourceAddress,
                    closedContext.ValidFrom,
                    closedInterval.ValidUntil,
                    closedContext.VlanId,
                    closedContext.Vrf,
                    closedContext.CorporateRouteTrace,
                    closedContext.InternetRouteTrace,
                    closedContext.WazuhRouteTrace);
            }
        }

        bool activeExists = _intervals.Values.Any(i => i.EndpointId.Equals(openedInterval.EndpointId) && i.IsActive);
        if (activeExists && closedInterval is null)
        {
            throw new DomainInvariantException(
                $"Endpoint '{openedInterval.EndpointId}' already has an active presence interval ({EndpointPresenceCodes.OverlappingActiveInterval}).");
        }

        _intervals[openedInterval.PresenceId.Value] = openedInterval;
        _contexts[routingContext.PresenceId.Value] = routingContext;
        return Task.CompletedTask;
    }
}

internal sealed class FakeResponseAssessmentStore : IResponseAssessmentStore
{
    private readonly Dictionary<Guid, ResponseAssessment> _byId = [];

    public Task<ResponseAssessment?> GetActiveByEndpointAsync(
        EndpointId endpointId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.Values.SingleOrDefault(a =>
            a.EndpointId.Equals(endpointId) && a.IsActive));

    public Task SaveAsync(ResponseAssessment assessment, CancellationToken cancellationToken = default)
    {
        _byId[assessment.AssessmentId.Value] = assessment;
        return Task.CompletedTask;
    }

    public void Seed(ResponseAssessment assessment) => _byId[assessment.AssessmentId.Value] = assessment;
}
