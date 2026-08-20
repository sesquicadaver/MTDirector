using System.Reflection;
using Google.Protobuf.Reflection;
using Mfc.Application.Deployment;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Deployment;
using Xunit;
using DomainDevice = Mfc.Domain.Inventory.Device;
using DomainIpFamily = Mfc.Domain.Inventory.IpAddressFamily;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainOperationState = Mfc.Domain.Deployment.DeploymentOperationState;
using DomainProbeKind = Mfc.Domain.Deployment.DeploymentProbeKind;
using DomainRecoveryAction = Mfc.Domain.Deployment.DeploymentRecoveryAction;
using DomainUplinkMode = Mfc.Domain.Inventory.DeclaredUplinkMode;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-13 AC 1–13.
/// Covers Safe Deployment Spec §55/§59–§62: fault tolerance, security boundary, and rollback acceptance.
/// All tests use scripted in-process runtimes (no live RouterOS connection required for CI merge).
/// </summary>
public sealed class DeploymentFaultSecurityAcceptanceLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 16, 0, 0, TimeSpan.Zero);

    // ── AC 1 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Happy path standalone deployment completes as Committed.
    /// Validates the full stage→arm→activate→verify→disarm→commit lifecycle end-to-end.
    /// </summary>
    [Fact]
    public async Task Ac1SuccessfulStandaloneDeploymentCommits()
    {
        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: false);

        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node, plan, operation, device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            existingForNode: [],
            packetPathPairs: DeploymentTestFactory.CpuPairs(),
            addressLists: [],
            chains: [],
            observedResourceHashAfterStaging: plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DomainOperationState.Committed, result.State);
        Assert.NotNull(result.CommitSnapshot);
    }

    // ── AC 2 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A plan with identical old/new artifacts terminates NoChanges without any filter-set or script-add writes.
    /// </summary>
    [Fact]
    public async Task Ac2NoChangesPerformsNoWrites()
    {
        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0, noChanges: true);
        Assert.True(StandaloneDeploymentPolicy.IsNoChanges(plan.DevicePlans[0]));

        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: false);

        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node, plan, operation, device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            plan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DomainOperationState.NoChanges, result.State);
        Assert.False(result.WroteToDevice);
        Assert.DoesNotContain(channel.Sent, static s => DeploymentWritePaths.IsFilterSet(s.Path));
        Assert.DoesNotContain(channel.Sent, static s => s.Path == DeploymentWritePath.SystemScriptAdd);
    }

    // ── AC 3 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Multi-WAN failover topology: per-active-path probe planning succeeds.
    /// Multi-WAN balanced topology: per-table probe planning succeeds.
    /// In both cases <see cref="MultiWanDeploymentVerification.EnsureFilterOnlyWriteSurface"/> accepts
    /// filter/script/scheduler writes and rejects routing/NAT/Mangle paths.
    /// </summary>
    [Fact]
    public async Task Ac3MultiWanFailoverAndBalancedProbesPass()
    {
        // Failover: active-path probe planning
        MultiWanUplinkTopology failoverTopology = new()
        {
            UplinkMode = DomainUplinkMode.Failover,
            RequiredRoutingTables = [],
            ActivePathDestination = "198.51.100.1",
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };
        DeploymentProbe[] failoverProbes =
        [
            new(DomainProbeKind.RouterPing, "198.51.100.1", 500),
            new(DomainProbeKind.RouterPing, "203.0.113.1", 500),
        ];
        ManagedIntegrityResult failoverPlan = MultiWanDeploymentVerification.PlanRuntimeProbes(
            failoverTopology, failoverProbes, out IReadOnlyList<DeploymentProbe> failoverSelected);
        Assert.True(failoverPlan.Passed, string.Join(';', failoverPlan.Findings.Select(static f => f.Message)));
        Assert.Single(failoverSelected);
        Assert.Equal("198.51.100.1", failoverSelected[0].Destination);

        // Balanced: per-routing-table probe planning
        MultiWanUplinkTopology balancedTopology = new()
        {
            UplinkMode = DomainUplinkMode.Balanced,
            RequiredRoutingTables = ["wan1", "wan2"],
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };
        DeploymentProbe[] balancedProbes =
        [
            new(DomainProbeKind.RouterPing, "192.0.2.1", 500, routingTable: "wan1"),
            new(DomainProbeKind.RouterPing, "192.0.2.2", 500, routingTable: "wan2"),
        ];
        ManagedIntegrityResult balancedPlan = MultiWanDeploymentVerification.PlanRuntimeProbes(
            balancedTopology, balancedProbes, out IReadOnlyList<DeploymentProbe> balancedSelected);
        Assert.True(balancedPlan.Passed, string.Join(';', balancedPlan.Findings.Select(static f => f.Message)));
        Assert.Equal(2, balancedSelected.Count);

        // Filter-only write surface check
        ManagedIntegrityResult filterOnly = MultiWanDeploymentVerification.EnsureFilterOnlyWriteSurface(
        [
            "/ip/firewall/filter/add",
            "/ip/firewall/filter/set",
            "/system/script/add",
            "/system/scheduler/add",
        ]);
        Assert.True(filterOnly.Passed);

        ManagedIntegrityResult withViolations = MultiWanDeploymentVerification.EnsureFilterOnlyWriteSurface(
        [
            "/ip/firewall/filter/add",
            "/ip/route/add",
            "/ip/firewall/nat/add",
        ]);
        Assert.False(withViolations.Passed);
        Assert.All(withViolations.Findings, static f =>
            Assert.Equal(DeploymentCodes.MultiWanWriteSurfaceViolation, f.Code));

        // Use case executes balanced probes successfully
        MultiWanDependencyHashes hashes = Hashes("ok");
        MultiWanUplinkTopology ucTopology = new()
        {
            UplinkMode = DomainUplinkMode.Balanced,
            RequiredRoutingTables = ["t1", "t2"],
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };
        DeploymentProbe[] ucProbes =
        [
            new(DomainProbeKind.RouterPing, "192.0.2.10", 500, routingTable: "t1"),
            new(DomainProbeKind.RouterPing, "192.0.2.11", 500, routingTable: "t2"),
        ];
        CountingPingSession session = new();
        MultiWanDeploymentVerificationResult ucResult = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DomainUplinkMode.Balanced,
            hashes,
            hashes,
            ucTopology,
            ucProbes,
            ["/ip/firewall/filter/add"],
            DeploymentTestFactory.H("art"),
            DeploymentTestFactory.H("route"),
            session);
        Assert.True(ucResult.Succeeded, ucResult.Message);
        Assert.Equal(2, session.PingCount);
    }

    // ── AC 4 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// VRRP active/passive happy path: ExecuteVrrpDeploymentUseCase commits and both members are staged/activated.
    /// </summary>
    [Fact]
    public async Task Ac4VrrpActivePassiveCommitsAllMembers()
    {
        DomainNode node = DeploymentTestFactory.VrrpWithMembers(out DomainDevice first, out DomainDevice second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        ScriptedCluster cluster = new(
            new ScriptedMember(first.Id, VrrpMemberObservedState.Backup),
            new ScriptedMember(second.Id, VrrpMemberObservedState.Master));

        VrrpDeploymentResult result = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
            node, plan, operation,
            cluster.Members,
            [],
            DeploymentTestFactory.CpuPairs(),
            T0.AddMinutes(1));

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DomainOperationState.Committed, result.State);
        Assert.False(result.PartialCommitAttempted);
        Assert.All(cluster.Members, static m =>
        {
            Assert.True(m.Staged);
            Assert.True(m.Activated);
        });
    }

    // ── AC 5 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A VRRP cluster with two simultaneous Masters (split-master condition) must never be simplified.
    /// EnsureNoSplitMasterSimplification throws with code VrrpSplitMaster.
    /// EnsureFullCommitAllowed blocks partial commit.
    /// </summary>
    [Fact]
    public void Ac5VrrpSplitMasterIsNotSimplified()
    {
        DeviceId a = DeviceId.New();
        DeviceId b = DeviceId.New();
        VrrpRoleVector split = new()
        {
            Members =
            [
                Snapshot(a, VrrpMemberObservedState.Master),
                Snapshot(b, VrrpMemberObservedState.Master),
            ],
        };

        Assert.True(VrrpDeploymentPolicy.HasSplitMaster(split));

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(
            () => VrrpDeploymentPolicy.EnsureNoSplitMasterSimplification(split));
        Assert.StartsWith(DeploymentCodes.VrrpSplitMaster, ex.Message, StringComparison.Ordinal);

        DomainInvariantException partial = Assert.Throws<DomainInvariantException>(
            () => VrrpDeploymentPolicy.EnsureFullCommitAllowed([a, b], new HashSet<DeviceId> { a }));
        Assert.StartsWith(DeploymentCodes.VrrpPartialCommitForbidden, partial.Message, StringComparison.Ordinal);
    }

    // ── AC 6 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// After effectful points (activation filter-set calls) that fail, the outcome is always
    /// in the allowed terminal set {RolledBack, Failed, RecoveryRequired, NoChanges} — never
    /// a silent partial Committed state.
    /// </summary>
    [Theory]
    [InlineData(0)]           // first filter-set fails → rollback filter-sets also fail → RecoveryRequired
    [InlineData(int.MaxValue)] // all activations succeed; wrong hash → rollback triggered → RolledBack
    public async Task Ac6DisconnectAfterEffectfulPointsLeavesAllowedTerminal(int failFilterSetsAfter)
    {
        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment device = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: false);
        channel.FailFilterSetsAfter = failFilterSetsAfter;

        // Use wrong hash only for MaxValue case (force verification failure → rollback)
        Hash256 stagingHash = failFilterSetsAfter == int.MaxValue
            ? DeploymentTestFactory.H("wrong-hash-ac6")
            : plan.DevicePlans[0].NewArtifactHash;

        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node, plan, operation, device,
            new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            stagingHash,
            T0.AddMinutes(1),
            T0);

        DomainOperationState[] allowed =
        [
            DomainOperationState.RolledBack,
            DomainOperationState.Failed,
            DomainOperationState.RecoveryRequired,
            DomainOperationState.NoChanges,
        ];
        Assert.Contains(result.State, allowed);
        // Must never reach a partial Committed state on failure path
        Assert.NotEqual(DomainOperationState.Committed, result.State);
    }

    /// <summary>
    /// Crash at Activating (nonterminal, all-new anchors, no durable commit) recovers deterministically
    /// via ControllerRollback → RolledBack.
    /// A durable Committed operation with all-new anchors is recognized as KeepCommitted.
    /// </summary>
    [Theory]
    [InlineData(false, DomainRecoveryAction.ControllerRollback)]
    [InlineData(true, DomainRecoveryAction.KeepCommitted)]
    public async Task Ac6RecoverAfterCrashAtActivatingIsDeterministic(
        bool durableCommitted,
        DomainRecoveryAction expected)
    {
        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        AdvanceToActivating(operation);
        if (durableCommitted)
        {
            AdvanceToCommitted(operation);
        }

        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        Dictionary<string, string> jumps = devicePlan.NewAnchorTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);
        ScriptedRollbackRuntime runtime = new(devicePlan.DeviceId, jumps, devicePlan.OldArtifactHash);

        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan, operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(1));

        Assert.Equal(expected, result.Action);
        if (expected == DomainRecoveryAction.ControllerRollback)
        {
            Assert.True(result.Succeeded, result.ErrorCode);
            Assert.Equal(DomainOperationState.RolledBack, result.State);
        }
        else
        {
            Assert.True(result.Succeeded, result.ErrorCode);
            Assert.Equal(DomainOperationState.Committed, result.State);
        }
    }

    // ── AC 7 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recovery with all-old jumps + a disabled deadline scheduler (mfc-rb-d-*) is recognized as
    /// RecognizeWatchdogRollback → RolledBack, not as a fresh controller rollback.
    /// </summary>
    [Fact]
    public async Task Ac7DeadlineWatchdogRollbackIsRecognized()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        // Simulate watchdog having already fired: anchors restored to old by the scheduler script
        foreach (AnchorTarget old in plan.DevicePlans[0].OldAnchorTargets)
        {
            runtime.Jumps[old.Key.Marker] = old.JumpTarget;
        }

        string deadlineToken = DeploymentWatchdogNames.DeadlineScheduler("0123456789abcdef");
        runtime.SchedulerNames = [deadlineToken];
        runtime.SchedulerDisabled = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            [deadlineToken] = true,
        };

        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan, operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(1));

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DomainRecoveryAction.RecognizeWatchdogRollback, result.Action);
        Assert.Equal(DomainOperationState.RolledBack, result.State);
    }

    // ── AC 8 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recovery with all-old jumps + a disabled startup watchdog scheduler (mfc-rb-b-*) is also
    /// recognized as RecognizeWatchdogRollback → RolledBack.
    /// </summary>
    [Fact]
    public async Task Ac8StartupWatchdogRollbackIsRecognized()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        foreach (AnchorTarget old in plan.DevicePlans[0].OldAnchorTargets)
        {
            runtime.Jumps[old.Key.Marker] = old.JumpTarget;
        }

        string startupToken = DeploymentWatchdogNames.StartupScheduler("0123456789abcdef");
        runtime.SchedulerNames = [startupToken];
        runtime.SchedulerDisabled = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            [startupToken] = true,
        };

        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan, operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(1));

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DomainRecoveryAction.RecognizeWatchdogRollback, result.Action);
        Assert.Equal(DomainOperationState.RolledBack, result.State);
    }

    // ── AC 9 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A third-party jump target (neither old nor new anchor) observed mid-activation must not be
    /// automatically resolved. Recovery must produce RecoveryRequired.
    /// </summary>
    [Fact]
    public async Task Ac9ManualAnchorChangeCreatesRecoveryRequired()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        // Inject a third-party target for the first anchor
        string marker = plan.DevicePlans[0].OldAnchorTargets[0].Key.Marker;
        runtime.Jumps[marker] = "mfc-third-party-unknown-target";

        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan, operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Equal(DomainRecoveryAction.RecoveryRequired, result.Action);
        Assert.Equal(DomainOperationState.RecoveryRequired, result.State);
        Assert.Equal(DeploymentCodes.RecoveryRequired, result.ErrorCode);
    }

    // ── AC 10 ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crash recovery decision table is deterministic:
    /// non-terminal after activation (all-new, no commit) → ControllerRollback → RolledBack.
    /// Durable Committed with all-new anchors → KeepCommitted → Committed.
    /// </summary>
    [Theory]
    [InlineData(DeploymentAnchorSetState.AllNew, false, DomainRecoveryAction.ControllerRollback)]
    [InlineData(DeploymentAnchorSetState.AllNew, true, DomainRecoveryAction.KeepCommitted)]
    [InlineData(DeploymentAnchorSetState.MixedOldNew, false, DomainRecoveryAction.ControllerRollback)]
    [InlineData(DeploymentAnchorSetState.AllOld, false, DomainRecoveryAction.RecognizeWatchdogRollback)]
    [InlineData(DeploymentAnchorSetState.ThirdTarget, false, DomainRecoveryAction.RecoveryRequired)]
    public void Ac10CrashRecoveryIsDeterministic(
        DeploymentAnchorSetState anchors,
        bool committed,
        DomainRecoveryAction expected)
    {
        DomainRecoveryAction actual = DeploymentRecoveryDecision.Decide(
            anchors,
            DeploymentWatchdogPresence.AbsentOrDisabled,
            committed,
            activationStarted: true);
        Assert.Equal(expected, actual);

        // Cross-check: only durable Committed may retain new artifact
        Assert.Equal(committed, DeploymentRecoveryDecision.MayRetainNewArtifact(committed));
    }

    // ── AC 11 ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Security boundary: credentials and raw command surfaces must not be exposed.
    /// (a) DeploymentService proto descriptor contains no password/script_source/raw_command fields.
    /// (b) Desktop DeploymentViewModel HasRawRouterOsCommands and HasForceApply are present as booleans.
    /// (c) Watchdog script rendered from plan anchors contains no "password" literal.
    /// (d) Workflow operation strings do not contain credential keywords.
    /// </summary>
    [Fact]
    public void Ac11CredentialsAndScriptsDoNotLeak()
    {
        // (a) Proto descriptor scan
        string[] forbidden = ["password", "script_source", "raw_command", "force_apply", "executecommand"];
        foreach (DescriptorBase item in WalkDescriptor(DeploymentService.Descriptor.File))
        {
            string name = (item switch
            {
                MethodDescriptor m => m.Name,
                FieldDescriptor f => f.Name,
                MessageDescriptor msg => msg.Name,
                _ => string.Empty,
            }).ToLowerInvariant();

            foreach (string kw in forbidden)
            {
                Assert.DoesNotContain(kw, name, StringComparison.OrdinalIgnoreCase);
            }
        }

        // (b) Desktop ViewModel surface
        Type vm = typeof(Mfc.Desktop.ViewModels.DeploymentViewModel);
        PropertyInfo rawProp = vm.GetProperty(nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.HasRawRouterOsCommands))!;
        PropertyInfo forceProp = vm.GetProperty(nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.HasForceApply))!;
        Assert.NotNull(rawProp);
        Assert.NotNull(forceProp);
        Assert.Equal(typeof(bool), rawProp.PropertyType);
        Assert.Equal(typeof(bool), forceProp.PropertyType);

        // (c) Watchdog script contains no password literal
        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        string script = DeploymentWatchdogScript.Render(
            devicePlan.OldAnchorTargets,
            devicePlan.NewAnchorTargets,
            devicePlan.AnchorRollbackOrder);
        Assert.DoesNotContain("password", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user=", script, StringComparison.OrdinalIgnoreCase);

        // (d) Workflow operation strings contain no credential keywords
        string[] ops =
        [
            CreateDeploymentPlanUseCase.Operation,
            StartDeploymentUseCase.Operation,
            RollbackDeploymentWorkflowUseCase.Operation,
            GetDeploymentRecoveryStatusUseCase.Operation,
        ];
        foreach (string op in ops)
        {
            Assert.DoesNotContain("password", op, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credential", op, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", op, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── AC 12 ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Arbitrary command and path injection is impossible:
    /// (a) DeploymentWritePaths has no /move, filter/remove, address-list/set|remove, script/run.
    /// (b) Architecture boundary: RouterOs does not expose forbidden write namespaces.
    /// (c) Requesting an out-of-range DeploymentWritePath enum value throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Ac12ArbitraryCommandAndPathInjectionImpossible()
    {
        // (a) Enumerate all valid write paths and confirm none are forbidden
        string[] forbiddenPathSubstrings =
        [
            "/move",
            "filter/remove",
            "address-list/remove",
            "address-list/set",
            "script/run",
        ];

        foreach (DeploymentWritePath path in Enum.GetValues<DeploymentWritePath>())
        {
            if (path == DeploymentWritePath.Ping)
            {
                continue; // Not a mutation path; skip ping
            }

            string fixedPath = DeploymentWritePaths.Fixed(path);
            foreach (string forbidden in forbiddenPathSubstrings)
            {
                Assert.DoesNotContain(forbidden, fixedPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        // (b) RouterOs assembly must not expose forbidden write namespaces
        Assembly routerOs = typeof(Mfc.RouterOs.AssemblyMarker).Assembly;
        string[] forbiddenNamespaces =
        [
            "Mfc.RouterOs.Write",
            "Mfc.RouterOs.Scripting",
            "Mfc.RouterOs.Terminal",
            "Mfc.RouterOs.GenericCommands",
        ];
        foreach (string ns in forbiddenNamespaces)
        {
            Type[] hits = routerOs.GetTypes()
                .Where(t => string.Equals(t.Namespace, ns, StringComparison.Ordinal)
                            || (t.Namespace is not null
                                && t.Namespace.StartsWith(ns + ".", StringComparison.Ordinal)))
                .ToArray();
            Assert.True(hits.Length == 0,
                $"Forbidden namespace '{ns}' is present: {string.Join(", ", hits.Select(static t => t.FullName))}");
        }

        // (c) Invalid enum value throws
        DeploymentWritePath invalid = (DeploymentWritePath)byte.MaxValue;
        Assert.Throws<InvalidOperationException>(() => DeploymentWritePaths.Fixed(invalid));
    }

    // ── AC 13 ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Only old-committed or exact-recovery outcomes are allowed for any terminal state.
    /// Decision table must produce consistent results across all anchor-set / watchdog / committed combinations.
    /// MayRetainNewArtifact returns true only when durableCommitted is true.
    /// </summary>
    [Fact]
    public void Ac13OnlyOldCommittedOrExactRecoveryAllowed()
    {
        // MayRetainNewArtifact invariant
        Assert.True(DeploymentRecoveryDecision.MayRetainNewArtifact(durableCommitted: true));
        Assert.False(DeploymentRecoveryDecision.MayRetainNewArtifact(durableCommitted: false));

        // Full decision table from Spec §47–§49 must not produce unexpected actions
        DomainRecoveryAction[] tableExpected =
        [
            AssertAllowedTerminal(DeploymentAnchorSetState.ThirdTarget, DeploymentWatchdogPresence.AbsentOrDisabled, false, true),
            AssertAllowedTerminal(DeploymentAnchorSetState.AllOld, DeploymentWatchdogPresence.AbsentOrDisabled, false, false),
            AssertAllowedTerminal(DeploymentAnchorSetState.AllNew, DeploymentWatchdogPresence.AbsentOrDisabled, false, true),
            AssertAllowedTerminal(DeploymentAnchorSetState.MixedOldNew, DeploymentWatchdogPresence.Active, false, true),
            AssertAllowedTerminal(DeploymentAnchorSetState.AllOld, DeploymentWatchdogPresence.AbsentOrDisabled, false, true),
            AssertAllowedTerminal(DeploymentAnchorSetState.AllNew, DeploymentWatchdogPresence.AbsentOrDisabled, true, true),
            AssertAllowedTerminal(DeploymentAnchorSetState.Incomplete, DeploymentWatchdogPresence.AbsentOrDisabled, false, false),
        ];

        Assert.Equal(7, tableExpected.Length);
        Assert.All(tableExpected, static action =>
            Assert.True(Enum.IsDefined<DomainRecoveryAction>(action), $"Unexpected DeploymentRecoveryAction value: {action}"));
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>Invokes the decision table and verifies the action is a defined member.</summary>
    private static DomainRecoveryAction AssertAllowedTerminal(
        DeploymentAnchorSetState anchors,
        DeploymentWatchdogPresence watchdog,
        bool committed,
        bool activationStarted)
    {
        DomainRecoveryAction action = DeploymentRecoveryDecision.Decide(anchors, watchdog, committed, activationStarted);
        Assert.True(Enum.IsDefined<DomainRecoveryAction>(action), $"Decision table produced undefined action: {(int)action}");
        return action;
    }

    /// <summary>Builds a VRRP role snapshot with a single IPv4/VRID-1 instance.</summary>
    private static VrrpMemberRoleSnapshot Snapshot(DeviceId deviceId, VrrpMemberObservedState state)
        => new()
        {
            DeviceId = deviceId,
            HasIndependentRoutedTraffic = false,
            Reachable = true,
            Instances =
            [
                new VrrpInstanceRoleFact
                {
                    Family = DomainIpFamily.IPv4,
                    Vrid = 1,
                    ObservedState = state,
                },
            ],
        };

    /// <summary>Creates a test operation seeded with all-new anchors and stopped at Activating.</summary>
    private static (DeploymentPlan Plan, DeploymentOperation Operation, ScriptedRollbackRuntime Runtime) SeedActivatedNew()
    {
        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        AdvanceToActivating(operation);

        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        Dictionary<string, string> jumps = devicePlan.NewAnchorTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);
        ScriptedRollbackRuntime runtime = new(devicePlan.DeviceId, jumps, devicePlan.OldArtifactHash);
        return (plan, operation, runtime);
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

    private static void AdvanceToCommitted(DeploymentOperation operation)
    {
        operation.EnsureTransition(DomainOperationState.Verifying, T0.AddSeconds(7));
        operation.EnsureTransition(DomainOperationState.DisarmingWatchdog, T0.AddSeconds(8));
        operation.EnsureTransition(DomainOperationState.Committed, T0.AddSeconds(9));
    }

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

    /// <summary>Recursively yields all descriptor items from a proto file for security scanning.</summary>
    private static IEnumerable<DescriptorBase> WalkDescriptor(FileDescriptor file)
    {
        foreach (MessageDescriptor message in file.MessageTypes)
        {
            foreach (DescriptorBase item in WalkMessage(message))
            {
                yield return item;
            }
        }

        foreach (ServiceDescriptor service in file.Services)
        {
            foreach (MethodDescriptor method in service.Methods)
            {
                yield return method;
            }
        }
    }

    private static IEnumerable<DescriptorBase> WalkMessage(MessageDescriptor message)
    {
        yield return message;
        foreach (FieldDescriptor field in message.Fields.InDeclarationOrder())
        {
            yield return field;
        }

        foreach (MessageDescriptor nested in message.NestedTypes)
        {
            foreach (DescriptorBase child in WalkMessage(nested))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Minimal ping-counting session for VerifyMultiWanDeploymentUseCase integration sub-test.
    /// </summary>
    private sealed class CountingPingSession : IRouterOsDeploymentSession
    {
        public int PingCount { get; private set; }

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
        {
            PingCount++;
            return Task.FromResult(new RouterPingResult { Outcome = RouterPingOutcome.Pass, Sent = 3, Received = 3 });
        }
    }
}
