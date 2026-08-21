using System.Reflection;
using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Drift;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Deployment;
using Mfc.UnitTests.Deployment;
using Xunit;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainOperationState = Mfc.Domain.Deployment.DeploymentOperationState;

namespace Mfc.UnitTests.E2E;

/// <summary>
/// Living Spec matrix for Issue Set M6-06 AC 1–10 (E2E Workflow Spec §55–§56).
/// Scripted in-process runtimes only — live CHR matrix remains OFF.
/// </summary>
public sealed class MultiWanE2ELivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);

    private const string PrimaryActivePath = "198.51.100.1";
    private const string BackupActivePath = "203.0.113.1";

    // ── AC 1 ──────────────────────────────────────────────────────────────────────

    /// <summary>Failover topology with primary WAN active: multi-WAN verify + filter-only deploy succeed.</summary>
    [Fact]
    public async Task Ac1FailoverWithPrimaryActiveSucceeds()
    {
        MultiWanDependencyHashes deps = Hashes("failover-primary");
        MultiWanUplinkTopology topology = FailoverTopology(PrimaryActivePath);
        DeploymentProbe[] probes =
        [
            new(DeploymentProbeKind.RouterPing, PrimaryActivePath, 500),
            new(DeploymentProbeKind.RouterPing, BackupActivePath, 500),
        ];
        RecordingChannel channel = await DeployMultiWanFilterOnlyAsync();
        string[] writeTokens = WritePathTokens(channel);
        CountingPingSession session = new();

        MultiWanDeploymentVerificationResult verify = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DeclaredUplinkMode.Failover,
            deps,
            deps,
            topology,
            probes,
            writeTokens,
            DeploymentTestFactory.H("artifact-primary"),
            DeploymentTestFactory.H("route-primary-active"),
            session);

        Assert.True(verify.Succeeded, verify.Message);
        Assert.False(verify.SkippedBecauseNotMultiWan);
        Assert.Equal(1, verify.MultiWanProbeCount);
        Assert.Equal(1, session.PingCount);
        Assert.Equal(PrimaryActivePath, session.LastDestination);
        Assert.Null(session.LastRoutingTable);
    }

    // ── AC 2 ──────────────────────────────────────────────────────────────────────

    /// <summary>Failover topology with backup WAN active: same verify path succeeds on the active path only.</summary>
    [Fact]
    public async Task Ac2FailoverWithBackupActiveSucceeds()
    {
        MultiWanDependencyHashes deps = Hashes("failover-backup");
        MultiWanUplinkTopology topology = FailoverTopology(BackupActivePath);
        DeploymentProbe[] probes =
        [
            new(DeploymentProbeKind.RouterPing, PrimaryActivePath, 500),
            new(DeploymentProbeKind.RouterPing, BackupActivePath, 500),
        ];
        CountingPingSession session = new();

        MultiWanDeploymentVerificationResult verify = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DeclaredUplinkMode.Failover,
            deps,
            deps,
            topology,
            probes,
            FilterOnlyWrites,
            DeploymentTestFactory.H("artifact-backup"),
            DeploymentTestFactory.H("route-backup-active"),
            session);

        Assert.True(verify.Succeeded, verify.Message);
        Assert.Equal(1, verify.MultiWanProbeCount);
        Assert.Equal(BackupActivePath, session.LastDestination);
        Assert.DoesNotContain(session.PingedDestinations, d => d == PrimaryActivePath);
    }

    // ── AC 3 ──────────────────────────────────────────────────────────────────────

    /// <summary>Sealed filter artifact is identical for primary-active and backup-active operational states.</summary>
    [Fact]
    public void Ac3ArtifactIdenticalForBothOperationalStates()
    {
        Hash256 artifact = DeploymentTestFactory.H("desired-filter-artifact");
        Hash256 primaryRoute = DeploymentTestFactory.H("obs-route-primary");
        Hash256 backupRoute = DeploymentTestFactory.H("obs-route-backup");
        Assert.NotEqual(primaryRoute, backupRoute);

        Hash256 viaPrimary = MultiWanDeploymentVerification.ArtifactHashIgnoringActiveRoute(artifact, primaryRoute);
        Hash256 viaBackup = MultiWanDeploymentVerification.ArtifactHashIgnoringActiveRoute(artifact, backupRoute);

        Assert.Equal(artifact, viaPrimary);
        Assert.Equal(artifact, viaBackup);
        Assert.Equal(viaPrimary, viaBackup);
    }

    // ── AC 4 ──────────────────────────────────────────────────────────────────────

    /// <summary>PCC / balanced topology verification succeeds (tables resolved, filter-only writes, no routing mutation).</summary>
    [Fact]
    public async Task Ac4PccTopologySucceeds()
    {
        FastTrackTopologyContext pccContext = FastTrackTopologyContext.From(
            TopologyDependencyFacts.Create(
                uplinkMode: DeclaredUplinkMode.Balanced,
                mangleRules:
                [
                    FacilityRuleFact.Create(
                        IpAddressFamily.IPv4,
                        0,
                        "prerouting",
                        "mark-routing",
                        perConnectionClassifier: "both-addresses:2/0",
                        newRoutingMark: "wan1"),
                ],
                routingTables:
                [
                    RoutingTableFact.Create("wan1"),
                    RoutingTableFact.Create("wan2"),
                ]));
        Assert.True(pccContext.HasPcc);
        Assert.Equal(DeclaredUplinkMode.Balanced, pccContext.UplinkMode);

        MultiWanDependencyHashes deps = Hashes("pcc");
        MultiWanUplinkTopology topology = new()
        {
            UplinkMode = DeclaredUplinkMode.Balanced,
            RequiredRoutingTables = ["wan1", "wan2"],
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };
        DeploymentProbe[] probes =
        [
            new(DeploymentProbeKind.RouterPing, "192.0.2.10", 500, routingTable: "wan1"),
            new(DeploymentProbeKind.RouterPing, "192.0.2.11", 500, routingTable: "wan2"),
        ];
        CountingPingSession session = new();

        MultiWanDeploymentVerificationResult verify = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DeclaredUplinkMode.Balanced,
            deps,
            deps,
            topology,
            probes,
            FilterOnlyWrites,
            DeploymentTestFactory.H("pcc-artifact"),
            DeploymentTestFactory.H("pcc-route-obs"),
            session);

        Assert.True(verify.Succeeded, verify.Message);
        Assert.Equal(2, verify.MultiWanProbeCount);
        Assert.Contains("wan1", session.PingedRoutingTables);
        Assert.Contains("wan2", session.PingedRoutingTables);
        Assert.True(MultiWanDeploymentVerification.EnsureFilterOnlyWriteSurface(FilterOnlyWrites).Passed);
    }

    // ── AC 5 ──────────────────────────────────────────────────────────────────────

    /// <summary>Balanced/mixed per-routing-table ROUTER_PING probes all execute.</summary>
    [Fact]
    public async Task Ac5PerTableProbesSucceed()
    {
        MultiWanDependencyHashes deps = Hashes("per-table");
        MultiWanUplinkTopology topology = new()
        {
            UplinkMode = DeclaredUplinkMode.Mixed,
            RequiredRoutingTables = ["t-main", "t-wan2"],
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };
        DeploymentProbe[] probes =
        [
            new(DeploymentProbeKind.RouterPing, "192.0.2.20", 500, routingTable: "t-main"),
            new(DeploymentProbeKind.RouterPing, "192.0.2.21", 500, routingTable: "t-wan2"),
            new(DeploymentProbeKind.RouterPing, "192.0.2.99", 500),
        ];
        CountingPingSession session = new();

        MultiWanDeploymentVerificationResult verify = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DeclaredUplinkMode.Mixed,
            deps,
            deps,
            topology,
            probes,
            FilterOnlyWrites,
            DeploymentTestFactory.H("mixed-art"),
            DeploymentTestFactory.H("mixed-route"),
            session);

        Assert.True(verify.Succeeded, verify.Message);
        Assert.Equal(2, verify.MultiWanProbeCount);
        Assert.Equal(2, session.PingCount);
        Assert.Equal(["t-main", "t-wan2"], session.PingedRoutingTables.OrderBy(static t => t, StringComparer.Ordinal).ToArray());
    }

    // ── AC 6 ──────────────────────────────────────────────────────────────────────

    /// <summary>Unsafe FastTrack (PCC / balanced / mixed) is blocked with CONTEXT_UNSUPPORTED.</summary>
    [Fact]
    public void Ac6FastTrackUnsafeCaseIsBlocked()
    {
        PolicyRule fastTrack = AllowedFastTrackRule();
        Dictionary<ServiceObjectId, ServiceObject> catalog = FastTrackCatalog();

        FastTrackAnalysisResult pcc = FastTrackAnalysis.Analyze(
            [fastTrack],
            FastTrackTopologyContext.Create(hasPcc: true),
            catalog);
        FastTrackAnalysisResult balanced = FastTrackAnalysis.Analyze(
            [fastTrack],
            FastTrackTopologyContext.Create(DeclaredUplinkMode.Balanced),
            catalog);
        FastTrackAnalysisResult mixed = FastTrackAnalysis.Analyze(
            [fastTrack],
            FastTrackTopologyContext.Create(DeclaredUplinkMode.Mixed),
            catalog);

        Assert.True(pcc.HasBlockers);
        Assert.True(balanced.HasBlockers);
        Assert.True(mixed.HasBlockers);
        Assert.Contains(pcc.Findings, static f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(balanced.Findings, static f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(mixed.Findings, static f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.False(pcc.AllowsSafeFastTrack);
        Assert.True(FastTrackAnalysisCodes.IsFailedPrecondition(FastTrackAnalysisCodes.ContextUnsupported));
    }

    // ── AC 7 ──────────────────────────────────────────────────────────────────────

    /// <summary>Multi-WAN path never writes Routing/NAT/Mangle; allowlist has no such DeploymentWritePath members.</summary>
    [Fact]
    public async Task Ac7RoutingNatMangleAreNotChanged()
    {
        RecordingChannel channel = await DeployMultiWanFilterOnlyAsync();
        string[] tokens = WritePathTokens(channel);
        ManagedIntegrityResult surface = MultiWanDeploymentVerification.EnsureFilterOnlyWriteSurface(tokens);
        Assert.True(surface.Passed, string.Join(';', surface.Findings.Select(static f => f.Message)));

        Assert.DoesNotContain(
            Enum.GetNames<DeploymentWritePath>(),
            static n => n.Contains("Nat", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Mangle", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Route", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Raw", StringComparison.OrdinalIgnoreCase));

        foreach (DeploymentWritePath path in Enum.GetValues<DeploymentWritePath>())
        {
            string fixedPath = DeploymentWritePaths.Fixed(path);
            Assert.False(
                fixedPath.Contains("/ip/route", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/firewall/nat", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/firewall/mangle", StringComparison.OrdinalIgnoreCase)
                || fixedPath.Contains("/firewall/raw", StringComparison.OrdinalIgnoreCase),
                fixedPath);
        }

        ManagedIntegrityResult dirty = MultiWanDeploymentVerification.EnsureFilterOnlyWriteSurface(
        [
            "/ip/firewall/filter/add",
            "/ip/route/add",
            "/ip/firewall/nat/add",
            "/ip/firewall/mangle/add",
        ]);
        Assert.False(dirty.Passed);
        Assert.Equal(3, dirty.Findings.Count);
    }

    // ── AC 8 ──────────────────────────────────────────────────────────────────────

    /// <summary>Forced failover is rejected; no ForceFailover API on multi-WAN verification surface.</summary>
    [Fact]
    public async Task Ac8ForcedFailoverIsNotPerformed()
    {
        MultiWanUplinkTopology forced = FailoverTopology(PrimaryActivePath) with { ForcedFailoverRequested = true };
        ManagedIntegrityResult rejected = MultiWanDeploymentVerification.PlanRuntimeProbes(
            forced,
            [new DeploymentProbe(DeploymentProbeKind.RouterPing, PrimaryActivePath, 500)],
            out IReadOnlyList<DeploymentProbe> selected);
        Assert.False(rejected.Passed);
        Assert.Empty(selected);
        Assert.Equal(DeploymentCodes.MultiWanForcedFailoverForbidden, rejected.Findings[0].Code);

        MultiWanDeploymentVerificationResult useCase = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DeclaredUplinkMode.Failover,
            Hashes("forced"),
            Hashes("forced"),
            forced,
            [new DeploymentProbe(DeploymentProbeKind.RouterPing, PrimaryActivePath, 500)],
            FilterOnlyWrites,
            DeploymentTestFactory.H("art"),
            DeploymentTestFactory.H("route"));
        Assert.False(useCase.Succeeded);
        Assert.Equal(DeploymentCodes.MultiWanForcedFailoverForbidden, useCase.Code);

        Assert.Null(typeof(VerifyMultiWanDeploymentUseCase).GetMethod(
            "ForceFailover",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        Assert.Null(typeof(MultiWanDeploymentVerification).GetMethod(
            "ForceFailover",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        Assert.DoesNotContain(
            typeof(VerifyMultiWanDeploymentUseCase).GetMethods(BindingFlags.Public | BindingFlags.Static),
            static m => m.Name.Contains("ForceFailover", StringComparison.OrdinalIgnoreCase)
                        || m.Name.Contains("DisablePrimary", StringComparison.OrdinalIgnoreCase));
    }

    // ── AC 9 ──────────────────────────────────────────────────────────────────────

    /// <summary>Dependency hash change voids the sealed plan (rollback required; deploy verify fails closed).</summary>
    [Fact]
    public async Task Ac9DependencyChangeVoidsOrCancelsThePlan()
    {
        MultiWanDependencyHashes planned = Hashes("plan-seal");
        MultiWanDependencyHashes observed = planned with
        {
            RoutingConfigHash = DeploymentTestFactory.H("routing-changed-after-seal"),
            NatHash = DeploymentTestFactory.H("nat-changed-after-seal"),
        };

        ManagedIntegrityResult recheck = MultiWanDeploymentVerification.RecheckDependencyHashes(planned, observed);
        Assert.False(recheck.Passed);
        Assert.True(recheck.RequiresRollback);
        Assert.Contains(recheck.Findings, static f => f.Target == "routing");
        Assert.Contains(recheck.Findings, static f => f.Target == "nat");
        Assert.All(recheck.Findings, static f => Assert.Equal(DeploymentCodes.MultiWanDependencyDrift, f.Code));

        MultiWanDeploymentVerificationResult verify = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DeclaredUplinkMode.Failover,
            planned,
            observed,
            FailoverTopology(PrimaryActivePath),
            [new DeploymentProbe(DeploymentProbeKind.RouterPing, PrimaryActivePath, 500)],
            FilterOnlyWrites,
            DeploymentTestFactory.H("art"),
            DeploymentTestFactory.H("route"));
        Assert.False(verify.Succeeded);
        Assert.True(verify.RequiresRollback);
        Assert.Equal(DeploymentCodes.MultiWanDependencyDrift, verify.Code);
        Assert.Equal(0, verify.MultiWanProbeCount);
    }

    // ── AC 10 ─────────────────────────────────────────────────────────────────────

    /// <summary>Active WAN / route observation is not configuration drift and does not alter artifact identity.</summary>
    [Fact]
    public void Ac10ActiveRouteChangeDoesNotCreateConfigurationDrift()
    {
        Hash256 committed = DeploymentTestFactory.H("committed-filter");
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            committed,
            committed,
            desiredArtifactHash: committed,
            [new DriftFinding(DriftFindingKind.ActiveWanChanged, "primary→backup")]);

        Assert.Equal(DriftOutcome.ObservationOnly, evaluation.Outcome);
        Assert.False(evaluation.ConfigurationDriftPresent);
        Assert.False(evaluation.BlocksDeployment);
        Assert.Equal(DriftSeverity.Observation, DriftClassifier.Classify(DriftFindingKind.ActiveWanChanged));

        Hash256 viaA = MultiWanDeploymentVerification.ArtifactHashIgnoringActiveRoute(
            committed,
            DeploymentTestFactory.H("route-a"));
        Hash256 viaB = MultiWanDeploymentVerification.ArtifactHashIgnoringActiveRoute(
            committed,
            DeploymentTestFactory.H("route-b"));
        Assert.Equal(committed, viaA);
        Assert.Equal(viaA, viaB);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static readonly string[] FilterOnlyWrites =
    [
        "/ip/firewall/filter/add",
        "/ip/firewall/filter/set",
        "/system/script/add",
        "/system/scheduler/add",
    ];

    private static MultiWanDependencyHashes Hashes(string seed)
        => new()
        {
            RoutingConfigHash = DeploymentTestFactory.H(seed + ":routing"),
            RoutingRuleHash = DeploymentTestFactory.H(seed + ":rr"),
            NatHash = DeploymentTestFactory.H(seed + ":nat"),
            RawHash = DeploymentTestFactory.H(seed + ":raw"),
            MangleHash = DeploymentTestFactory.H(seed + ":mangle"),
            ZoneResolutionHash = DeploymentTestFactory.H(seed + ":zone"),
            InterfaceListMembershipHash = DeploymentTestFactory.H(seed + ":il"),
            RpFilterHash = DeploymentTestFactory.H(seed + ":rp"),
        };

    private static MultiWanUplinkTopology FailoverTopology(string activePath)
        => new()
        {
            UplinkMode = DeclaredUplinkMode.Failover,
            RequiredRoutingTables = [],
            ActivePathDestination = activePath,
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };

    private static async Task<RecordingChannel> DeployMultiWanFilterOnlyAsync()
    {
        DomainNode node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("mw-e2e"),
            NodeKind.Router,
            DeclaredUplinkMode.Failover);
        _ = node.AddDevice(
            NonEmptyName.Create("mw-dev"),
            ManagementEndpoint.Create("10.255.40.10"),
            DeviceRole.Router);

        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: false);

        StandaloneDeploymentResult deployed = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            deviceState,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);

        Assert.True(deployed.Succeeded, deployed.ErrorCode);
        Assert.Equal(DomainOperationState.Committed, deployed.State);
        return channel;
    }

    private static string[] WritePathTokens(RecordingChannel channel)
        => channel.Sent
            .Select(static s => DeploymentWritePaths.Fixed(s.Path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static Dictionary<ServiceObjectId, ServiceObject> FastTrackCatalog()
    {
        ServiceObject tcp = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("tcp-mw"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "tcp"))]);
        return new Dictionary<ServiceObjectId, ServiceObject> { [tcp.Id] = tcp };
    }

    private static PolicyRule AllowedFastTrackRule()
    {
        Dictionary<ServiceObjectId, ServiceObject> catalog = FastTrackCatalog();
        ServiceObjectId tcpId = catalog.Keys.Single();
        return PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            0,
            TrafficPredicate.Create(
                services: ServiceSelector.Create([tcpId]),
                connectionStates: [ConnectionState.Established, ConnectionState.Related],
                serviceCatalog: catalog),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept),
            LogSpecification.Disabled);
    }

    private sealed class CountingPingSession : IRouterOsDeploymentSession
    {
        public int PingCount { get; private set; }

        public string? LastDestination { get; private set; }

        public string? LastRoutingTable { get; private set; }

        public List<string> PingedDestinations { get; } = [];

        public List<string> PingedRoutingTables { get; } = [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<ActualManagedState> ReadManagedStateAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWriteExecutionResult> AddAddressListEntryAsync(
            AddressListEntryWrite write,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWriteExecutionResult> AddFilterRuleAsync(
            FilterRuleWrite write,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
            AnchorTargetWrite write,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWriteExecutionResult> AddRollbackScriptAsync(
            RollbackScriptWrite write,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWriteExecutionResult> AddRollbackSchedulerAsync(
            RollbackSchedulerWrite write,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWriteExecutionResult> DisableRollbackSchedulerAsync(
            RouterOsItemId schedulerId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWriteExecutionResult> RemoveRollbackSchedulerAsync(
            RouterOsItemId schedulerId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentWriteExecutionResult> RemoveRollbackScriptAsync(
            RouterOsItemId scriptId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RouterPingResult> PingAsync(RouterPingRequest request, CancellationToken cancellationToken = default)
        {
            PingCount++;
            LastDestination = request.Destination.ToString();
            LastRoutingTable = request.RoutingTable;
            PingedDestinations.Add(LastDestination);
            if (request.RoutingTable is not null)
            {
                PingedRoutingTables.Add(request.RoutingTable);
            }

            return Task.FromResult(new RouterPingResult
            {
                Outcome = RouterPingOutcome.Pass,
                Sent = 3,
                Received = 3,
            });
        }
    }
}
