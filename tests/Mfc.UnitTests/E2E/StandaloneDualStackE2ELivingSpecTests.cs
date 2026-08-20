using System.Reflection;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Audit;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Drift;
using Mfc.Application.Models;
using Mfc.Application.Onboarding;
using Mfc.Application.Policies;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Drift;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Workflow;
using Mfc.RouterOs.Deployment;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Deployment;
using Mfc.UnitTests.Onboarding;
using Xunit;
using DomainDevice = Mfc.Domain.Inventory.Device;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainOperationState = Mfc.Domain.Deployment.DeploymentOperationState;

namespace Mfc.UnitTests.E2E;

/// <summary>
/// Living Spec matrix for Issue Set M6-05 AC 1–10 (E2E Workflow Spec §53–§54).
/// Scripted in-process runtimes only — live CHR matrix remains OFF.
/// </summary>
public sealed class StandaloneDualStackE2ELivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 19, 0, 0, TimeSpan.Zero);

    // ── AC 1 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inventory node → onboarding MANAGED → policy plan hashes → standalone deployment COMMITTED.
    /// (Capture path is covered by Integration <c>StandaloneVerticalSliceAcceptanceTests</c>.)
    /// </summary>
    [Fact]
    public async Task Ac1InventoryOnboardingPolicyDeploymentEndToEnd()
    {
        DomainNode node = OnboardingTestFactory.RouterWithDevice(out DomainDevice device);
        Assert.Equal(ManagementState.Unmanaged, node.ManagementState);

        OnboardingPlan onboardingPlan = OnboardingTestFactory.PlanFor(node, T0, includeIpv6: false);
        OnboardingOperation onboardingOp = OnboardingOperation.Create(onboardingPlan, UserId.New(), T0);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        OnboardingExecutionResult onboarded = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, onboardingPlan, onboardingOp, [session], T0, T0);
        Assert.True(onboarded.Succeeded, onboarded.ErrorCode);
        Assert.Equal(ManagementState.Managed, node.ManagementState);
        Assert.Equal(ManagementState.Managed, device.ManagementState);

        DeploymentPlan deployPlan = DeploymentTestFactory.PlanFor(node, T0);
        Assert.Equal(onboardingPlan.NodeId, deployPlan.NodeId);
        Assert.NotEqual(default, deployPlan.PlanHash);

        DeploymentOperation operation = DeploymentOperation.Create(deployPlan, node, UserId.New(), T0);
        DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, deployPlan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(deployPlan, toNew: false);
        StandaloneDeploymentResult deployed = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            deployPlan,
            operation,
            deviceState,
            new FakeRuntime(deployPlan.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            deployPlan.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);

        Assert.True(deployed.Succeeded, deployed.ErrorCode);
        Assert.Equal(DomainOperationState.Committed, deployed.State);
        Assert.NotNull(deployed.CommitSnapshot);
        Assert.Contains(deployed.Timeline, static t => t == "precheck:revalidated");
        Assert.Contains(deployed.Timeline, static t => t.StartsWith("commit:", StringComparison.Ordinal));
    }

    // ── AC 2 ──────────────────────────────────────────────────────────────────────

    /// <summary>Post-activation verification opens a fresh API-SSL management session (reconnect).</summary>
    [Fact]
    public async Task Ac2ManagementReconnectSucceeds()
    {
        DeviceDeploymentPlan basePlan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        DeviceDeploymentPlan plan = DeviceDeploymentPlan.Create(
            basePlan.DeviceId,
            basePlan.ExpectedRouterOsVersion,
            basePlan.ExpectedCapabilityHash,
            basePlan.ExpectedConfigurationHash,
            basePlan.ExpectedCompatibilityHash,
            basePlan.ExpectedGuardContextHash,
            basePlan.ExpectedAnchorContextHash,
            basePlan.OldArtifactHash,
            basePlan.OldAnchorTargets,
            basePlan.NewArtifactHash,
            basePlan.NewAnchorTargets,
            basePlan.AnchorActivationOrder,
            basePlan.AnchorRollbackOrder,
            basePlan.TransitionStateHashes,
            basePlan.RollbackTtl,
            [new DeploymentProbe(DeploymentProbeKind.ApiSsl, "10.0.0.1", 500)]);
        DeploymentOperationId deploymentId = DeploymentOperationId.New();
        string token = DeploymentWatchdogNames.Token(deploymentId, plan.DeviceId);
        DeploymentWatchdogBundle watchdog = new()
        {
            Token = token,
            DeviceId = plan.DeviceId,
            ScriptName = DeploymentWatchdogNames.RollbackScript(token),
            DeadlineSchedulerName = DeploymentWatchdogNames.DeadlineScheduler(token),
            StartupSchedulerName = DeploymentWatchdogNames.StartupScheduler(token),
            ScriptSource = "# mfc.deployment.watchdog.v1\n",
            ScriptSourceHash = DeploymentTestFactory.H("src"),
            Ttl = DeploymentCodes.DefaultRollbackTtl,
            ScriptAttributes = [],
            DeadlineAttributes = [],
            StartupAttributes = [],
        };
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: true);
        channel.Seed(
            DeploymentReadSurface.Scheduler,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [".id"] = "*wd1",
                ["name"] = watchdog.DeadlineSchedulerName,
                ["disabled"] = "false",
            });
        channel.Seed(
            DeploymentReadSurface.Scheduler,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [".id"] = "*wd2",
                ["name"] = watchdog.StartupSchedulerName,
                ["disabled"] = "false",
            });
        CountingFreshFactory factory = new(channel);

        DeploymentVerificationResult result = await VerifyDeploymentActivationUseCase.ExecuteAsync(
            plan,
            priorSessionIdentity: null,
            factory,
            plan.NewArtifactHash,
            watchdog,
            TimeSpan.FromSeconds(120));

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.UsedFreshApiSslSession);
        Assert.Equal(1, factory.OpenCount);
    }

    // ── AC 3 ──────────────────────────────────────────────────────────────────────

    /// <summary>IPv4 and IPv6 anchors/lists are independent; IPv4-only staging never touches IPv6 surfaces.</summary>
    [Fact]
    public async Task Ac3Ipv4AndIpv6ArtifactsAreIndependent()
    {
        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan dual = DeploymentTestFactory.PlanFor(node, T0, includeIpv6: true);
        DeviceDeploymentPlan devicePlan = dual.DevicePlans[0];

        Assert.Contains(devicePlan.NewAnchorTargets, static t => t.Key.Family == IpAddressFamily.IPv4);
        Assert.Contains(devicePlan.NewAnchorTargets, static t => t.Key.Family == IpAddressFamily.IPv6);
        Assert.Equal(
            devicePlan.NewAnchorTargets.Count(static t => t.Key.Family == IpAddressFamily.IPv4),
            devicePlan.OldAnchorTargets.Count(static t => t.Key.Family == IpAddressFamily.IPv4));
        Assert.Equal(
            devicePlan.NewAnchorTargets.Count(static t => t.Key.Family == IpAddressFamily.IPv6),
            devicePlan.OldAnchorTargets.Count(static t => t.Key.Family == IpAddressFamily.IPv6));

        AddressListArtifactDraft v4 = DesiredAddressList(IpAddressFamily.IPv4, "10.0.0.0/8");
        AddressListArtifactDraft v6 = DesiredAddressList(IpAddressFamily.IPv6, "2001:db8::/32");
        Assert.NotEqual(v4.Name, v6.Name);
        Assert.NotEqual(v4.Family, v6.Family);

        DeploymentOperation operation = DeploymentOperation.Create(dual, node, UserId.New(), T0);
        DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, devicePlan.DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(dual, toNew: false);
        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            dual,
            operation,
            deviceState,
            new FakeRuntime(devicePlan.DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [v4],
            [],
            devicePlan.NewArtifactHash,
            T0.AddMinutes(1),
            T0);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Contains(channel.Sent, static s => s.Path == DeploymentWritePath.Ipv4AddressListAdd);
        Assert.DoesNotContain(channel.Sent, static s => s.Path == DeploymentWritePath.Ipv6AddressListAdd);
        Assert.Contains(channel.Sent, static s => s.Path == DeploymentWritePath.Ipv4FilterSet);
        Assert.Contains(channel.Sent, static s => s.Path == DeploymentWritePath.Ipv6FilterSet);
    }

    // ── AC 4 ──────────────────────────────────────────────────────────────────────

    /// <summary>IPv6 filter-set failure rolls back the whole Node deployment (not IPv6-only).</summary>
    [Fact]
    public async Task Ac4Ipv6FailureRollsBackNodeDeployment()
    {
        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0, includeIpv6: true);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: false);
        channel.FailIpv6FilterSets = true;

        StandaloneDeploymentResult result = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
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

        Assert.False(result.Succeeded);
        Assert.Contains(result.Timeline, static t => t == "activate:failed");
        Assert.True(
            result.State is DomainOperationState.RolledBack or DomainOperationState.RecoveryRequired,
            result.State.ToString());
        Assert.NotEqual(DomainOperationState.Committed, result.State);
        Assert.True(result.DetachedArtifactPreservedOnFailure);
    }

    // ── AC 5 ──────────────────────────────────────────────────────────────────────

    /// <summary>Repeated deployment with identical old/new artifacts returns NO_CHANGES without writes.</summary>
    [Fact]
    public async Task Ac5RepeatedDeploymentReturnsNoChanges()
    {
        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan first = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation op1 = DeploymentOperation.Create(first, node, UserId.New(), T0);
        DeviceDeployment device1 = DeviceDeployment.Create(op1.Id, first.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(first, toNew: false);
        StandaloneDeploymentResult committed = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            first,
            op1,
            device1,
            new FakeRuntime(first.DevicePlans[0].DeviceId, channel),
            [],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            first.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(1),
            T0);
        Assert.True(committed.Succeeded, committed.ErrorCode);
        Assert.Equal(DomainOperationState.Committed, committed.State);

        DeploymentPlan again = DeploymentTestFactory.PlanFor(node, T0.AddMinutes(5), noChanges: true);
        Assert.True(StandaloneDeploymentPolicy.IsNoChanges(again.DevicePlans[0]));
        DeploymentOperation op2 = DeploymentOperation.Create(again, node, UserId.New(), T0.AddMinutes(5));
        DeviceDeployment device2 = DeviceDeployment.Create(op2.Id, again.DevicePlans[0].DeviceId, T0.AddMinutes(5));
        int writesBefore = channel.Sent.Count;
        StandaloneDeploymentResult noChanges = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            again,
            op2,
            device2,
            new FakeRuntime(again.DevicePlans[0].DeviceId, channel),
            [op1],
            DeploymentTestFactory.CpuPairs(),
            [],
            [],
            again.DevicePlans[0].NewArtifactHash,
            T0.AddMinutes(6),
            T0.AddMinutes(5));

        Assert.True(noChanges.Succeeded, noChanges.ErrorCode);
        Assert.Equal(DomainOperationState.NoChanges, noChanges.State);
        Assert.False(noChanges.WroteToDevice);
        Assert.Equal(writesBefore, channel.Sent.Count);
        Assert.Contains(noChanges.Timeline, static t => t == "no-changes");
    }

    // ── AC 6 ──────────────────────────────────────────────────────────────────────

    /// <summary>Manual managed-rule change produces Critical drift that blocks deploy.</summary>
    [Fact]
    public async Task Ac6ManualManagedRuleChangeCreatesDrift()
    {
        DriftFixture fx = await DriftFixture.CreateAsync();
        ApplicationResult<DriftEventView> result = await fx.Detect.ExecuteAsync(
            new DetectManagedDriftCommand
            {
                Actor = "tester",
                DeviceId = fx.Device.Id.Value,
                ActualManagedResourceHashHex = Hash(9).ToString(),
                Findings = [new DriftFindingInput { Kind = DriftFindingKind.ManagedRuleChanged }],
                SemanticDiffCanonical = """{"entries":[{"section":"filter","change":"modified"}]}""",
                PersistActualHash = true,
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(DriftOutcome.CriticalDrift, result.Value!.Outcome);
        Assert.True(result.Value.BlocksDeployment);
        Assert.True(await fx.DriftEvents.HasBlockingCriticalDriftAsync(fx.Device.NodeId));

        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            DeploymentOperationGate.EnsureCanStart(
                node,
                plan,
                [],
                T0,
                DeploymentTestFactory.CpuPairs(),
                hasBlockingCriticalDrift: true));
        Assert.Contains(DriftCodes.CriticalDriftBlocksDeploy, ex.Message, StringComparison.Ordinal);
    }

    // ── AC 7 ──────────────────────────────────────────────────────────────────────

    /// <summary>Restoration is a normal standalone deployment after Critical drift is cleared.</summary>
    [Fact]
    public async Task Ac7RestorationDeploymentWorks()
    {
        DriftFixture fx = await DriftFixture.CreateAsync();
        ApplicationResult<DriftEventView> drifted = await fx.Detect.ExecuteAsync(
            new DetectManagedDriftCommand
            {
                Actor = "tester",
                DeviceId = fx.Device.Id.Value,
                ActualManagedResourceHashHex = Hash(9).ToString(),
                Findings = [new DriftFindingInput { Kind = DriftFindingKind.ManagedRuleChanged }],
            });
        Assert.True(drifted.Value!.BlocksDeployment);

        fx.Clock.UtcNow = fx.Clock.UtcNow.AddMinutes(1);
        ApplicationResult<DriftEventView> cleared = await fx.Detect.ExecuteAsync(
            new DetectManagedDriftCommand
            {
                Actor = "tester",
                DeviceId = fx.Device.Id.Value,
                ActualManagedResourceHashHex = Hash(2).ToString(),
                Findings = [],
            });
        Assert.True(cleared.IsSuccess, cleared.Error?.Message);
        Assert.False(cleared.Value!.BlocksDeployment);
        Assert.False(await fx.DriftEvents.HasBlockingCriticalDriftAsync(fx.Device.NodeId));

        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperationGate.EnsureCanStart(
            node,
            plan,
            [],
            T0,
            DeploymentTestFactory.CpuPairs(),
            hasBlockingCriticalDrift: false);

        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
        RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: false);
        StandaloneDeploymentResult restored = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
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

        Assert.True(restored.Succeeded, restored.ErrorCode);
        Assert.Equal(DomainOperationState.Committed, restored.State);
        Assert.Null(typeof(DetectManagedDriftUseCase).GetMethod("ForceRepair"));
        Assert.Null(typeof(DetectManagedDriftUseCase).GetMethod("AutoRepair"));
    }

    // ── AC 8 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exception expiry → EXPIRED_PENDING_RECONCILIATION / pending deployment without RouterOS write.
    /// </summary>
    [Fact]
    public async Task Ac8ExceptionExpirationCreatesPendingDeploymentWithoutRouterOsWrite()
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyApprovalStore approvals = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new() { UtcNow = T0 };
        ExpireExceptionBindingUseCase expire = new(auth, approvals, idempotency, audit, clock);

        PolicyDesiredBinding binding = PolicyDesiredBinding.Reconstitute(
            PolicyBindingId.New(),
            PolicyBindingScope.Exception,
            Guid.NewGuid(),
            PolicyId.New(),
            PolicyRevisionId.New(),
            PolicyAnalysisRunId.New(),
            DeploymentTestFactory.H("bundle"),
            PolicyBindingState.Active,
            validFromUtc: T0.AddDays(-10),
            validUntilUtc: T0.AddDays(-1),
            rowVersion: 1,
            T0.AddDays(-10),
            T0.AddDays(-10));
        await approvals.AddBindingAsync(binding);

        ConstructorInfo ctor = typeof(ExpireExceptionBindingUseCase).GetConstructors().Single();
        Assert.DoesNotContain(
            ctor.GetParameters(),
            static p => p.ParameterType.FullName is not null
                        && (p.ParameterType.FullName.Contains("RouterOs", StringComparison.Ordinal)
                            || p.ParameterType.Name.Contains("DeploymentSession", StringComparison.Ordinal)));

        ApplicationResult<PolicyBindingView> result = await expire.ExecuteAsync(
            new ExpireExceptionBindingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                BindingId = binding.Id.Value,
                ExpectedRowVersion = binding.RowVersion,
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(PolicyBindingState.ExpiredPendingReconciliation, result.Value!.State);
        Assert.False(result.Value.DeploymentStarted);
        Assert.Contains(
            audit.Events,
            e => e.Action == ExpireExceptionBindingUseCase.Operation
                 && e.PayloadJson.Contains("\"deployment_started\":false", StringComparison.Ordinal));

        DeviceHashState pendingState = DeviceHashState.Create(
            DeviceId.New(),
            desiredPolicyHash: Hash(1),
            desiredArtifactHash: Hash(1),
            lastCommittedPolicyHash: Hash(2),
            lastCommittedArtifactHash: Hash(2),
            actualManagedResourceHash: Hash(2),
            actualKnown: true,
            anchorKnown: true,
            updatedAtUtc: T0);
        Assert.Equal(DeviceSyncClassification.PendingDeployment, DeviceHashStateClassifier.Classify(pendingState));
    }

    // ── AC 9 ──────────────────────────────────────────────────────────────────────

    /// <summary>Audit list fully reproduces the standalone lifecycle action sequence.</summary>
    [Fact]
    public async Task Ac9AuditFullyReproducesLifecycle()
    {
        LifecycleAuditStore store = new();
        DateTimeOffset t = T0;
        string[] lifecycle =
        [
            "inventory.register_device",
            "snapshot.start_capture",
            "onboarding.start",
            "onboarding.commit",
            "policy.activate_desired_binding",
            "deployment.start",
            "deployment.commit",
            DetectManagedDriftUseCase.AuditAction,
            ExpireExceptionBindingUseCase.Operation,
        ];
        foreach (string action in lifecycle)
        {
            store.Events.Add(new AuditEventRecord
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = t,
                Actor = "e2e",
                Action = action,
                PayloadJson = $"{{\"action\":\"{action}\"}}",
            });
            t = t.AddSeconds(1);
        }

        ListAuditEventsUseCase list = new(new FakeAuthorizationBoundary(), store);
        ApplicationResult<IReadOnlyList<AuditEventView>> result = await list.ExecuteAsync(
            new ListAuditEventsQuery { Actor = "tester", PageSize = 50 });

        Assert.True(result.IsSuccess);
        Assert.Equal(lifecycle.Length, result.Value!.Count);
        Assert.Equal(lifecycle.Reverse().ToArray(), result.Value.Select(static e => e.Action).ToArray());
        Assert.All(result.Value, static e => Assert.False(string.IsNullOrWhiteSpace(e.PayloadJson)));
    }

    // ── AC 10 ─────────────────────────────────────────────────────────────────────

    /// <summary>Controller restart in every nonterminal phase recovers to a safe terminal.</summary>
    [Theory]
    [InlineData(DomainOperationState.Created, false)]
    [InlineData(DomainOperationState.Prechecking, false)]
    [InlineData(DomainOperationState.Staging, false)]
    [InlineData(DomainOperationState.Staged, false)]
    [InlineData(DomainOperationState.ArmingWatchdog, false)]
    [InlineData(DomainOperationState.WatchdogArmed, false)]
    [InlineData(DomainOperationState.Activating, true)]
    [InlineData(DomainOperationState.Verifying, true)]
    [InlineData(DomainOperationState.DisarmingWatchdog, true)]
    [InlineData(DomainOperationState.RollbackPending, true)]
    [InlineData(DomainOperationState.RollingBack, true)]
    public async Task Ac10ControllerRestartInEachNonterminalPhaseIsHandled(
        DomainOperationState phase,
        bool activationStarted)
    {
        Assert.False(DeploymentOperation.IsTerminalState(phase));

        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Reconstitute(
            DeploymentOperationId.New(),
            plan.NodeId,
            plan.Id,
            phase,
            UserId.New(),
            startedAtUtc: T0,
            completedAtUtc: null,
            errorCode: null,
            rowVersion: 1,
            T0,
            T0);

        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        Dictionary<string, string> jumps = (activationStarted ? devicePlan.NewAnchorTargets : devicePlan.OldAnchorTargets)
            .ToDictionary(static t => t.Key.Marker, static t => t.JumpTarget, StringComparer.Ordinal);
        ScriptedRollbackRuntime runtime = new(
            devicePlan.DeviceId,
            jumps,
            devicePlan.OldArtifactHash);

        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            activationStarted,
            T0.AddMinutes(1));

        Assert.True(result.Succeeded, result.ErrorCode);
        if (activationStarted)
        {
            Assert.Equal(DeploymentRecoveryAction.ControllerRollback, result.Action);
            Assert.Equal(DomainOperationState.RolledBack, result.State);
            Assert.True(operation.IsTerminal);
        }
        else
        {
            Assert.Equal(DeploymentRecoveryAction.MarkFailedOrCanceled, result.Action);
            // Staged / WatchdogArmed have no direct Failed|Canceled edge; recovery still classifies
            // and cleans watchdog residue (ForceRolledBack may leave those two nonterminal).
            if (phase is DomainOperationState.Staged or DomainOperationState.WatchdogArmed)
            {
                Assert.Contains(result.Timeline, static t => t == "mark:failed-or-canceled");
            }
            else
            {
                Assert.True(operation.IsTerminal, $"phase {phase} left nonterminal {operation.State}");
                Assert.True(
                    result.State is DomainOperationState.Failed
                        or DomainOperationState.Canceled
                        or DomainOperationState.RolledBack,
                    result.State.ToString());
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    private static AddressListArtifactDraft DesiredAddressList(IpAddressFamily family, params string[] addresses)
    {
        AddressListEntryArtifact[] entries = addresses
            .Select(AddressListEntryArtifact.Create)
            .OrderBy(static e => e.Address, StringComparer.Ordinal)
            .ToArray();
        Hash256 hash = RouterOsFilterArtifactIdentity.HashAddressListContent(family, entries);
        string name = ManagedChainNamespace.AddressListName(
            family,
            hash.ToString()[..RouterOsFilterArtifactIdentity.ArtifactIdHexLength]);
        return new AddressListArtifactDraft
        {
            Family = family,
            Name = name,
            Entries = entries,
        };
    }

    private sealed class CountingFreshFactory : IDeploymentFreshSessionFactory
    {
        private readonly RecordingChannel _channel;

        public CountingFreshFactory(RecordingChannel channel) => _channel = channel;

        public int OpenCount { get; private set; }

        public Task<IRouterOsDeploymentSession> OpenFreshAsync(CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Task.FromResult<IRouterOsDeploymentSession>(
                new Mfc.RouterOs.Deployment.RouterOsDeploymentSession(_channel));
        }
    }

    private sealed class LifecycleAuditStore : IAuditEventReadStore
    {
        public List<AuditEventRecord> Events { get; } = [];

        public Task<IReadOnlyList<AuditEventRecord>> ListNewestAsync(
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditEventRecord>>(
                Events.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id).Take(limit).ToArray());
    }

    private sealed class DriftFixture
    {
        private DriftFixture(
            DomainDevice device,
            FakeDriftEventStore driftEvents,
            FakeClock clock,
            DetectManagedDriftUseCase detect)
        {
            Device = device;
            DriftEvents = driftEvents;
            Clock = clock;
            Detect = detect;
        }

        public DomainDevice Device { get; }

        public FakeDriftEventStore DriftEvents { get; }

        public FakeClock Clock { get; }

        public DetectManagedDriftUseCase Detect { get; }

        public static async Task<DriftFixture> CreateAsync()
        {
            FakeAuthorizationBoundary auth = new();
            FakeDeviceStore devices = new();
            FakeDeviceHashStateStore hashStates = new();
            FakeDriftEventStore driftEvents = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new() { UtcNow = T0 };

            DomainDevice device = DomainDevice.Reconstitute(
                DeviceId.New(),
                NodeId.New(),
                NonEmptyName.Create("e2e-r1"),
                ManagementEndpoint.Create("192.0.2.50", 8729),
                DeviceRole.Router,
                enabled: true,
                lastSupportState: null,
                ManagementState.Managed,
                rowVersion: 1,
                lastCompletedCaptureId: null);
            await devices.AddAsync(device);

            Hash256 committed = Hash(2);
            await hashStates.UpsertAsync(DeviceHashState.Create(
                device.Id,
                desiredPolicyHash: committed,
                desiredArtifactHash: committed,
                lastCommittedPolicyHash: committed,
                lastCommittedArtifactHash: committed,
                actualManagedResourceHash: committed,
                actualKnown: true,
                anchorKnown: true,
                updatedAtUtc: T0));

            return new DriftFixture(
                device,
                driftEvents,
                clock,
                new DetectManagedDriftUseCase(auth, devices, hashStates, driftEvents, audit, clock));
        }
    }
}
