using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Deployment;
using Mfc.Controller;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.Domain.Workflow;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.Infrastructure.Persistence.Snapshots;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainDevice = Mfc.Domain.Inventory.Device;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainOperationState = Mfc.Domain.Deployment.DeploymentOperationState;

namespace Mfc.IntegrationTests.Security;

/// <summary>
/// Integration acceptance for Issue Set M6-08 AC 11–14 (E2E Spec §52 backup/restore).
/// Uses Postgres Testcontainers + pg_dump/pg_restore from postgres:18-alpine
/// (host client may be older than the container server).
/// Operational jobs disabled; no live CHR; Development master key only.
/// </summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class SecurityBackupRestoreAcceptanceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 15, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public SecurityBackupRestoreAcceptanceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    /// <summary>AC11: PostgreSQL backup/restore round-trip succeeds via scripted pg_dump/pg_restore.</summary>
    [Fact]
    public async Task Ac11PostgresBackupRestoreSucceeds()
    {
        (string sourceCs, string restoreCs, Guid deviceId, string password) = await SeedAndRestoreAsync();
        await using WebApplication restored = BuildApp(restoreCs);
        await using AsyncServiceScope scope = restored.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        Assert.Equal(1, await db.Devices.CountAsync(d => d.Id == deviceId));
        Assert.True(await db.EncryptedSecrets.AnyAsync());
        Assert.True(await db.AuditEvents.AnyAsync());

        EncryptedSecretEntity secret = await db.EncryptedSecrets.SingleAsync();
        string cipherAsText = Encoding.UTF8.GetString(secret.Ciphertext);
        Assert.DoesNotContain(password, cipherAsText, StringComparison.Ordinal);
        _ = sourceCs;
    }

    /// <summary>AC12: Snapshots after restore pass content-hash verification.</summary>
    [Fact]
    public async Task Ac12SnapshotsAfterRestorePassHashVerification()
    {
        (_, string restoreCs, Guid deviceId, _) = await SeedAndRestoreAsync();
        await using WebApplication restored = BuildApp(restoreCs);
        await using AsyncServiceScope scope = restored.Services.CreateAsyncScope();
        ISnapshotStore store = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        StoredSnapshotPage page = await store.ListByDevicePageAsync(new DeviceId(deviceId), limit: 10, cursor: null);
        StoredSnapshot snapshot = Assert.Single(page.Items);
        Assert.NotNull(snapshot.ConfigurationPayloadHash);

        byte[] payloadHashBytes = snapshot.ConfigurationPayloadHash!.Bytes.ToArray();
        SnapshotPayloadEntity payload = await db.SnapshotPayloads.SingleAsync(
            p => p.PayloadHash == payloadHashBytes);
        byte[] verified = BrotliPayloadCodec.DecodeAndVerify(
            payload.CompressedPayload,
            (SnapshotCompression)payload.Compression,
            payload.UncompressedSize,
            payload.PayloadHash);
        Assert.Contains("m6-08-restore", Encoding.UTF8.GetString(verified), StringComparison.Ordinal);
    }

    /// <summary>AC13: Active artifact references (device_hash_states → filter_artifacts) survive restore.</summary>
    [Fact]
    public async Task Ac13ActiveArtifactReferencesAreRestored()
    {
        (_, string restoreCs, Guid deviceId, _) = await SeedAndRestoreAsync();
        await using WebApplication restored = BuildApp(restoreCs);
        await using AsyncServiceScope scope = restored.Services.CreateAsyncScope();
        IDeviceHashStateStore hashStates = scope.ServiceProvider.GetRequiredService<IDeviceHashStateStore>();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        DeviceHashState? state = await hashStates.GetAsync(new DeviceId(deviceId));
        Assert.NotNull(state);
        Assert.NotNull(state.LastCommittedArtifactHash);

        byte[] resourceHash = state.LastCommittedArtifactHash.Bytes.ToArray();
        FilterArtifactEntity artifact = await db.FilterArtifacts.SingleAsync(a => a.ResourceHash == resourceHash);
        Assert.Equal(deviceId, artifact.DeviceId);
        Assert.True(state.LastCommittedArtifactHash.Bytes.SequenceEqual(artifact.ResourceHash));
    }

    /// <summary>AC14: Nonterminal operations after restore go through recovery.</summary>
    [Fact]
    public async Task Ac14NonterminalOperationsAfterRestoreGoThroughRecovery()
    {
        (_, string restoreCs, Guid deviceId, _) = await SeedAndRestoreAsync();
        await using WebApplication restored = BuildApp(restoreCs);
        await using AsyncServiceScope scope = restored.Services.CreateAsyncScope();
        IDeploymentStore deployments = scope.ServiceProvider.GetRequiredService<IDeploymentStore>();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        DeviceEntity device = await db.Devices.SingleAsync(d => d.Id == deviceId);
        DomainNode? node = await scope.ServiceProvider.GetRequiredService<INodeStore>().GetAsync(new NodeId(device.NodeId));
        Assert.NotNull(node);

        IReadOnlyList<DeploymentOperation> nonterminal = await deployments.ListNonterminalByNodeAsync(node.Id);
        DeploymentOperation operation = Assert.Single(nonterminal);
        Assert.False(operation.IsTerminal);
        Assert.Equal(DomainOperationState.Activating, operation.State);

        DeploymentPlan? plan = await deployments.GetPlanAsync(operation.PlanId);
        Assert.NotNull(plan);
        DeviceDeploymentPlan devicePlan = Assert.Single(plan.DevicePlans);
        Dictionary<string, string> jumps = devicePlan.NewAnchorTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);
        ScriptedRestoreRollbackRuntime runtime = new(devicePlan.DeviceId, jumps, devicePlan.OldArtifactHash);

        DeploymentRecoveryResult recovery = await RecoverDeploymentUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(5));

        Assert.True(recovery.Succeeded, recovery.ErrorCode);
        Assert.Equal(DeploymentRecoveryAction.ControllerRollback, recovery.Action);
        Assert.Equal(DomainOperationState.RolledBack, recovery.State);
    }

    private async Task<(string SourceCs, string RestoreCs, Guid DeviceId, string Password)> SeedAndRestoreAsync()
    {
        string sourceCs = await _postgres.CreateFreshDatabaseAsync();
        const string password = "m6-08-restore-secret-must-not-leak";
        Guid deviceId;

        await using (WebApplication app = BuildApp(sourceCs))
        {
            await app.Services.MigrateAsync();
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
            IConnectionProfileService profiles = scope.ServiceProvider.GetRequiredService<IConnectionProfileService>();
            ISnapshotStore snapshots = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();
            IDeviceHashStateStore hashStates = scope.ServiceProvider.GetRequiredService<IDeviceHashStateStore>();
            IDeploymentStore deployments = scope.ServiceProvider.GetRequiredService<IDeploymentStore>();
            ISiteStore sites = scope.ServiceProvider.GetRequiredService<ISiteStore>();
            INodeStore nodes = scope.ServiceProvider.GetRequiredService<INodeStore>();
            IDeviceStore devices = scope.ServiceProvider.GetRequiredService<IDeviceStore>();
            IAuditEventWriter audit = scope.ServiceProvider.GetRequiredService<IAuditEventWriter>();

            (DomainNode node, DomainDevice device, DeploymentPlan plan) = await SeedRouterAsync(sites, nodes, devices);
            deviceId = device.Id.Value;

            await profiles.UpsertAsync(new UpsertConnectionProfileCommand
            {
                DeviceId = deviceId,
                Username = "mfc-read",
                PasswordUtf8 = Encoding.UTF8.GetBytes(password),
                TrustMode = CertificateTrustMode.InternalCa,
                CaProfileRef = "lab-ca",
                Actor = "m6-08@test",
            });

            byte[] body = Encoding.UTF8.GetBytes("""{"m6-08-restore":true,"section":"system"}""");
            byte[] digest = Enumerable.Repeat((byte)0xA8, 32).ToArray();
            Hash256 hash = Hash256.Create(digest);
            await snapshots.PersistCompletedAsync(new SnapshotPersistRequest
            {
                DeviceId = device.Id,
                RequestedBy = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                IdempotencyKey = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Capture = new SnapshotCaptureResult
                {
                    ConfigurationHash = ConfigurationHash.FromDigest(hash),
                    ObservationHash = ObservationHash.FromDigest(hash),
                    CapabilityHash = CapabilityHash.FromDigest(hash),
                    SnapshotHash = SnapshotHash.FromDigest(hash),
                    SchemaVersion = 1,
                    RawPayload = body,
                    ConfigurationPayload = body,
                    ObservationPayload = body,
                    CapabilityPayload = body,
                    Sections = [],
                },
                CapturedAtUtc = T0,
            });

            Hash256 artifactHash = H("m6-08-active-artifact");
            BrotliPayloadCodec.EncodedPayload encoded = BrotliPayloadCodec.Encode(
                Encoding.UTF8.GetBytes("""{"artifact":"m6-08"}"""));
            db.FilterArtifacts.Add(new FilterArtifactEntity
            {
                ResourceHash = artifactHash.Bytes.ToArray(),
                ArtifactId = "0123456789abcdef",
                DeviceId = deviceId,
                PhysicalSemanticsHash = H("phys").Bytes.ToArray(),
                CompilerProfileHash = H("profile").Bytes.ToArray(),
                LogicalEffectivePolicyHash = H("logical").Bytes.ToArray(),
                DeviceResolvedPolicyHash = H("resolved").Bytes.ToArray(),
                AnalysisBundleHash = H("analysis").Bytes.ToArray(),
                CapabilityHash = H("cap").Bytes.ToArray(),
                CompilerVersion = "m6.08",
                CompiledAtUtc = T0,
                Compression = (short)encoded.Compression,
                UncompressedSize = encoded.UncompressedSize,
                CompressedPayload = encoded.CompressedPayload,
                CreatedAtUtc = T0,
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await hashStates.UpsertAsync(DeviceHashState.Create(
                device.Id,
                desiredPolicyHash: H("desired-policy"),
                desiredArtifactHash: artifactHash,
                lastCommittedPolicyHash: H("committed-policy"),
                lastCommittedArtifactHash: artifactHash,
                actualManagedResourceHash: artifactHash,
                actualKnown: true,
                anchorKnown: true,
                updatedAtUtc: T0));

            await deployments.AddPlanAsync(plan);
            DeploymentOperation operation = DeploymentOperation.Create(plan, UserId.New(), T0);
            await deployments.AddOperationAsync(operation);
            AdvanceToActivating(operation);
            await deployments.SaveOperationAsync(operation);

            await audit.AppendAsync(
                "m6-08@test",
                "security.backup.seed",
                """{"seed":true,"issue":"M6-08"}""");

            AuditEventEntity seeded = await db.AuditEvents.OrderByDescending(e => e.OccurredAtUtc).FirstAsync();
            byte[] expected = ComputeAuditEventHash(
                seeded.PreviousEventHash,
                seeded.Actor,
                seeded.Action,
                seeded.PayloadJson);
            Assert.True(expected.AsSpan().SequenceEqual(seeded.EventHash));
        }

        string restoreCs = await _postgres.CreateFreshDatabaseAsync();
        string expectedSeedHashHex;
        await using (WebApplication beforeDump = BuildApp(sourceCs))
        {
            await using AsyncServiceScope scope = beforeDump.Services.CreateAsyncScope();
            MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
            AuditEventEntity seed = await db.AuditEvents.SingleAsync(e => e.Action == "security.backup.seed");
            expectedSeedHashHex = Convert.ToHexString(seed.EventHash);
        }

        await _postgres.DumpAndRestoreAsync(sourceCs, restoreCs);

        await using (WebApplication verify = BuildApp(restoreCs))
        {
            await using AsyncServiceScope scope = verify.Services.CreateAsyncScope();
            MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
            List<AuditEventEntity> events = await db.AuditEvents
                .OrderBy(e => e.OccurredAtUtc)
                .ThenBy(e => e.Id)
                .ToListAsync();
            Assert.NotEmpty(events);
            Assert.Contains(events, static e => e.Action == "security.backup.seed");
            AuditEventEntity seedEvent = events.Single(static e => e.Action == "security.backup.seed");
            Assert.Equal(expectedSeedHashHex, Convert.ToHexString(seedEvent.EventHash));
            HashSet<string> allHashes = new(
                events.Select(static e => Convert.ToHexString(e.EventHash)),
                StringComparer.Ordinal);
            foreach (AuditEventEntity row in events)
            {
                if (row.PreviousEventHash is not null)
                {
                    Assert.Contains(Convert.ToHexString(row.PreviousEventHash), allHashes);
                }

                Assert.DoesNotContain(password, row.PayloadJson, StringComparison.OrdinalIgnoreCase);
            }
        }

        return (sourceCs, restoreCs, deviceId, password);
    }

    /// <summary>Mirrors <c>EfAuditEventWriter</c> event hash preimage (M6-08 Living Spec AC10).</summary>
    private static byte[] ComputeAuditEventHash(
        byte[]? previous,
        string actor,
        string action,
        string payloadJson)
        => SHA256.HashData(Encoding.UTF8.GetBytes($"{previous?.Length ?? 0}|{actor}|{action}|{payloadJson}"));

    private static async Task<(DomainNode Node, DomainDevice Device, DeploymentPlan Plan)> SeedRouterAsync(
        ISiteStore sites,
        INodeStore nodes,
        IDeviceStore devices)
    {
        Site site = Site.Create(SiteCode.Create("S608"), NonEmptyName.Create("Security restore lab"));
        await sites.AddAsync(site);
        DomainNode node = Node.Create(site.Id, NonEmptyName.Create("edge"), NodeKind.Router, DeclaredUplinkMode.One);
        DomainDevice device = node.AddDevice(
            NonEmptyName.Create("edge-dev"),
            ManagementEndpoint.Create("10.60.8.1"),
            DeviceRole.Router);
        await nodes.AddAsync(node);
        await devices.AddAsync(device);
        return (node, device, PlanFor(node, device));
    }

    private static DeploymentPlan PlanFor(DomainNode node, DomainDevice device)
    {
        IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(NodeKind.Router, includeIpv6: false);
        IReadOnlyList<AnchorKey> activation = DeploymentAnchorOrder.Sort(keys);
        List<AnchorTarget> oldTargets = [];
        List<AnchorTarget> newTargets = [];
        foreach (AnchorKey key in activation)
        {
            oldTargets.Add(new AnchorTarget(key, BootstrapArtifact.RootChainName(key.Family, key.Chain)));
            newTargets.Add(new AnchorTarget(
                key,
                $"mfc{(key.Family == IpAddressFamily.IPv4 ? "4" : "6")}.{AnchorKey.ChainCode(key.Chain)}.r.0123456789abcdef"));
        }

        TransitionStateValidationResult transitions = TransitionStateValidator.Validate(
            activation,
            oldTargets,
            newTargets,
            TransitionStateValidator.AllSafeEvidence(activation.Count));
        Assert.False(transitions.HasBlockers);

        DeviceDeploymentPlan devicePlan = DeviceDeploymentPlan.Create(
            device.Id,
            "7.16.2",
            H("cap"),
            H("cfg"),
            H("compat"),
            H("guard-ctx"),
            H("anchor-ctx"),
            H("old-art"),
            oldTargets,
            H("new-art"),
            newTargets,
            activation,
            activation.Reverse().ToArray(),
            transitions.TransitionStateHashes,
            DeploymentCodes.DefaultRollbackTtl,
            [new DeploymentProbe(DeploymentProbeKind.RouterPing, "192.0.2.1", 500)]);
        return DeploymentPlan.Create(
            node,
            H("policy"),
            H("analysis"),
            H("topology"),
            [devicePlan],
            UserId.New(),
            T0);
    }

    private static void AdvanceToActivating(DeploymentOperation operation)
    {
        operation.EnsureTransition(DomainOperationState.Prechecking, T0.AddSeconds(1));
        operation.EnsureTransition(DomainOperationState.Staging, T0.AddSeconds(2));
        operation.EnsureTransition(DomainOperationState.Staged, T0.AddSeconds(3));
        operation.EnsureTransition(DomainOperationState.ArmingWatchdog, T0.AddSeconds(4));
        operation.EnsureTransition(DomainOperationState.WatchdogArmed, T0.AddSeconds(5));
        operation.EnsureTransition(DomainOperationState.Activating, T0.AddSeconds(6));
    }

    private static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static WebApplication BuildApp(string connectionString)
    {
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        return Program.BuildHost(
            [
                "--environment", "Development",
                $"--Mfc:Grpc:ListenAddress={url}",
                "--Mfc:Grpc:AllowInsecureLoopback=true",
                "--Mfc:Grpc:ShutdownTimeoutSeconds=5",
                "--Mfc:Security:RequireTls=true",
                "--Mfc:Security:MasterKeyProvider=Development",
                "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
                "--Mfc:OperationalJobs:Enabled=false",
                $"--Mfc:Database:ConnectionString={connectionString}",
            ]);
    }

    private static int GetFreeTcpPort()
    {
        System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Minimal rollback runtime for post-restore recovery (no live RouterOS).</summary>
    private sealed class ScriptedRestoreRollbackRuntime : IDeploymentRollbackDeviceRuntime
    {
        public ScriptedRestoreRollbackRuntime(
            DeviceId deviceId,
            Dictionary<string, string> jumps,
            Hash256 observedResourceHash)
        {
            DeviceId = deviceId;
            Jumps = jumps;
            ObservedResourceHash = observedResourceHash;
        }

        public DeviceId DeviceId { get; }

        public Dictionary<string, string> Jumps { get; }

        public Hash256 ObservedResourceHash { get; set; }

        public Task<IReadOnlyDictionary<string, string>> ReadAnchorJumpsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyDictionary<string, string>)new Dictionary<string, string>(Jumps, StringComparer.Ordinal));

        public Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
            AnchorTargetWrite write,
            CancellationToken cancellationToken = default)
        {
            Jumps[write.OwnershipMarker] = write.JumpTarget;
            return Task.FromResult(new DeploymentWriteExecutionResult
            {
                Succeeded = true,
                Path = "/ip/firewall/filter/set",
                SentAttributes = [],
                ReadBack = new Dictionary<string, string>(StringComparer.Ordinal),
            });
        }

        public Task<Hash256> ReadManagedResourceHashAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ObservedResourceHash);

        public Task<IDeploymentFreshSessionFactory> CreateFreshSessionFactoryAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IDeploymentFreshSessionFactory>(new NullFreshFactory());

        public Task<RouterPingResult> ProbeAsync(DeploymentProbe probe, CancellationToken cancellationToken = default)
            => Task.FromResult(new RouterPingResult { Outcome = RouterPingOutcome.Pass, Sent = 3, Received = 3 });

        public Task DisarmAndCleanupWatchdogAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<(IReadOnlyList<string> SchedulerNames, IReadOnlyDictionary<string, bool> SchedulerDisabled)>
            ReadWatchdogSchedulerFactsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<string>, IReadOnlyDictionary<string, bool>)>(
                ([], new Dictionary<string, bool>(StringComparer.Ordinal)));

        private sealed class NullFreshFactory : IDeploymentFreshSessionFactory
        {
            public Task<IRouterOsDeploymentSession> OpenFreshAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IRouterOsDeploymentSession>(new NullFreshSession());
        }

        /// <summary>Fresh API-SSL handshake stub; rollback only opens/disposes the session.</summary>
        private sealed class NullFreshSession : IRouterOsDeploymentSession
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public Task<ActualManagedState> ReadManagedStateAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<DeploymentWriteExecutionResult> AddAddressListEntryAsync(
                AddressListEntryWrite write, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<DeploymentWriteExecutionResult> AddFilterRuleAsync(
                FilterRuleWrite write, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
                AnchorTargetWrite write, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<DeploymentWriteExecutionResult> AddRollbackScriptAsync(
                RollbackScriptWrite write, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<DeploymentWriteExecutionResult> AddRollbackSchedulerAsync(
                RollbackSchedulerWrite write, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<DeploymentWriteExecutionResult> DisableRollbackSchedulerAsync(
                RouterOsItemId schedulerId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<DeploymentWriteExecutionResult> RemoveRollbackSchedulerAsync(
                RouterOsItemId schedulerId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<DeploymentWriteExecutionResult> RemoveRollbackScriptAsync(
                RouterOsItemId scriptId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<RouterPingResult> PingAsync(RouterPingRequest request, CancellationToken cancellationToken = default)
                => Task.FromResult(new RouterPingResult { Outcome = RouterPingOutcome.Pass, Sent = 3, Received = 3 });
        }
    }
}
