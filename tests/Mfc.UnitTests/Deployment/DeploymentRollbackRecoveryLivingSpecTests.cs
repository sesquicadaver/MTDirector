using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-11 AC 1–12 (Safe Deployment Spec §46–§49).
/// </summary>
public sealed class DeploymentRollbackRecoveryLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ac1RollbackUsesReverseActivationOrder()
    {
        DeviceId a = DeviceId.New();
        DeviceId b = DeviceId.New();
        DeviceId c = DeviceId.New();
        IReadOnlyList<DeviceId> order = DeploymentRecoveryDecision.DeviceRollbackOrder([a, b, c]);
        Assert.Equal([c, b, a], order);
    }

    [Fact]
    public void Ac2AnchorTargetMustBeOldOrNew()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        Dictionary<string, string> jumps = plan.OldAnchorTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);
        Assert.Equal(
            DeploymentAnchorSetState.AllOld,
            DeploymentRecoveryDecision.ClassifyAnchors(plan.OldAnchorTargets, plan.NewAnchorTargets, jumps));

        jumps[plan.OldAnchorTargets[0].Key.Marker] = "mfc-third-party-target";
        Assert.Equal(
            DeploymentAnchorSetState.ThirdTarget,
            DeploymentRecoveryDecision.ClassifyAnchors(plan.OldAnchorTargets, plan.NewAnchorTargets, jumps));
    }

    [Fact]
    public async Task Ac3OldArtifactHashIsVerified()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        runtime.ObservedResourceHash = DeploymentTestFactory.H("not-old");
        DeploymentRollbackResult result = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.OldArtifactHashMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task Ac4NewApiConnectionOpensAfterRollback()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        DeploymentRollbackResult result = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            T0.AddMinutes(1));
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.True(result.UsedFreshApiSslSession);
        Assert.True(runtime.FreshOpened);
        Assert.Contains(result.Timeline, static t => t == "fresh-api-ssl:opened");
    }

    [Fact]
    public async Task Ac5OldStateProbesPass()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        DeploymentRollbackResult result = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            T0.AddMinutes(1));
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t.StartsWith("old-state-probe:ok:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac6MixedOldNewCompletesToAllOld()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        // Leave first anchor on new, force others old → mixed then rollback finishes all-old.
        string firstMarker = plan.DevicePlans[0].NewAnchorTargets[0].Key.Marker;
        foreach (AnchorTarget old in plan.DevicePlans[0].OldAnchorTargets.Skip(1))
        {
            runtime.Jumps[old.Key.Marker] = old.JumpTarget;
        }

        Assert.Equal(
            DeploymentAnchorSetState.MixedOldNew,
            DeploymentRecoveryDecision.ClassifyAnchors(
                plan.DevicePlans[0].OldAnchorTargets,
                plan.DevicePlans[0].NewAnchorTargets,
                runtime.Jumps));
        _ = firstMarker;

        DeploymentRollbackResult result = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            T0.AddMinutes(1));
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(
            DeploymentAnchorSetState.AllOld,
            DeploymentRecoveryDecision.ClassifyAnchors(
                plan.DevicePlans[0].OldAnchorTargets,
                plan.DevicePlans[0].NewAnchorTargets,
                runtime.Jumps));
    }

    [Fact]
    public async Task Ac7ThirdTargetCreatesRecoveryRequired()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        runtime.Jumps[plan.DevicePlans[0].OldAnchorTargets[0].Key.Marker] = "totally-unknown";
        DeploymentRollbackResult result = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.RecoveryRequired, result.ErrorCode);
        Assert.Equal(DeploymentOperationState.RecoveryRequired, result.State);
    }

    [Fact]
    public async Task Ac8WatchdogRollbackIsRecognized()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        foreach (AnchorTarget old in plan.DevicePlans[0].OldAnchorTargets)
        {
            runtime.Jumps[old.Key.Marker] = old.JumpTarget;
        }

        runtime.SchedulerNames = ["mfc-rb-d-0123456789abcdef"];
        runtime.SchedulerDisabled = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["mfc-rb-d-0123456789abcdef"] = true,
        };

        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(1));
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DeploymentRecoveryAction.RecognizeWatchdogRollback, result.Action);
        Assert.Equal(DeploymentOperationState.RolledBack, result.State);
    }

    [Fact]
    public async Task Ac9NonterminalAfterRestartIsRolledBack()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        // Crash mid-activation with all-new anchors and no durable commit.
        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(1));
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DeploymentRecoveryAction.ControllerRollback, result.Action);
        Assert.Equal(DeploymentOperationState.RolledBack, result.State);
    }

    [Fact]
    public async Task Ac10CrashAfterWatchdogDisableBeforeCommitRollsBack()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        // Simulate disarm-before-commit: anchors still new, watchdog already gone.
        runtime.SchedulerNames = [];
        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(1));
        Assert.Equal(DeploymentRecoveryAction.ControllerRollback, result.Action);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DeploymentOperationState.RolledBack, result.State);
        Assert.False(DeploymentRecoveryDecision.MayRetainNewArtifact(durableCommitted: false));
    }

    [Fact]
    public void Ac11OnlyDurableCommittedKeepsNewState()
    {
        Assert.True(DeploymentRecoveryDecision.MayRetainNewArtifact(true));
        Assert.False(DeploymentRecoveryDecision.MayRetainNewArtifact(false));
        Assert.Equal(
            DeploymentRecoveryAction.KeepCommitted,
            DeploymentRecoveryDecision.Decide(
                DeploymentAnchorSetState.AllNew,
                DeploymentWatchdogPresence.AbsentOrDisabled,
                committed: true,
                activationStarted: true));
        Assert.Equal(
            DeploymentRecoveryAction.ControllerRollback,
            DeploymentRecoveryDecision.Decide(
                DeploymentAnchorSetState.AllNew,
                DeploymentWatchdogPresence.AbsentOrDisabled,
                committed: false,
                activationStarted: true));
    }

    [Theory]
    [InlineData(DeploymentAnchorSetState.ThirdTarget, DeploymentWatchdogPresence.AbsentOrDisabled, false, true, DeploymentRecoveryAction.RecoveryRequired)]
    [InlineData(DeploymentAnchorSetState.AllOld, DeploymentWatchdogPresence.AbsentOrDisabled, false, false, DeploymentRecoveryAction.MarkFailedOrCanceled)]
    [InlineData(DeploymentAnchorSetState.AllNew, DeploymentWatchdogPresence.AbsentOrDisabled, false, true, DeploymentRecoveryAction.ControllerRollback)]
    [InlineData(DeploymentAnchorSetState.MixedOldNew, DeploymentWatchdogPresence.Active, false, true, DeploymentRecoveryAction.ControllerRollback)]
    [InlineData(DeploymentAnchorSetState.AllOld, DeploymentWatchdogPresence.AbsentOrDisabled, false, true, DeploymentRecoveryAction.RecognizeWatchdogRollback)]
    [InlineData(DeploymentAnchorSetState.AllNew, DeploymentWatchdogPresence.AbsentOrDisabled, true, true, DeploymentRecoveryAction.KeepCommitted)]
    [InlineData(DeploymentAnchorSetState.Incomplete, DeploymentWatchdogPresence.AbsentOrDisabled, false, false, DeploymentRecoveryAction.MarkFailedOrCanceled)]
    public void Ac12RecoveryDecisionTableIsComplete(
        DeploymentAnchorSetState anchors,
        DeploymentWatchdogPresence watchdog,
        bool committed,
        bool activationStarted,
        DeploymentRecoveryAction expected)
    {
        Assert.Equal(expected, DeploymentRecoveryDecision.Decide(anchors, watchdog, committed, activationStarted));
    }

    [Fact]
    public async Task PreActivationAllOldMarksFailedOrCanceled()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        operation.EnsureTransition(DeploymentOperationState.Prechecking, T0.AddSeconds(1));
        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        Dictionary<string, string> jumps = devicePlan.OldAnchorTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);
        ScriptedRollbackRuntime runtime = new(devicePlan.DeviceId, jumps, devicePlan.OldArtifactHash);
        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            activationStarted: false,
            T0.AddMinutes(1));
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(DeploymentRecoveryAction.MarkFailedOrCanceled, result.Action);
        Assert.Equal(DeploymentOperationState.Failed, result.State);
    }

    [Fact]
    public async Task DurableCommittedKeepsNewState()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        AdvanceToCommitted(operation);
        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        Dictionary<string, string> jumps = devicePlan.NewAnchorTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);
        ScriptedRollbackRuntime runtime = new(devicePlan.DeviceId, jumps, devicePlan.NewArtifactHash);
        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(1));
        Assert.True(result.Succeeded);
        Assert.Equal(DeploymentRecoveryAction.KeepCommitted, result.Action);
        Assert.Equal(DeploymentOperationState.Committed, result.State);
    }

    [Fact]
    public async Task RecoverThirdTargetRequiresRecovery()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        runtime.Jumps[plan.DevicePlans[0].OldAnchorTargets[0].Key.Marker] = "third";
        DeploymentRecoveryResult result = await RecoverDeploymentUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            activationStarted: true,
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentRecoveryAction.RecoveryRequired, result.Action);
        Assert.Equal(DeploymentCodes.RecoveryRequired, result.ErrorCode);
    }

    [Fact]
    public async Task RollbackRejectsCommittedOperation()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        AdvanceToCommitted(operation);
        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        ScriptedRollbackRuntime runtime = new(
            devicePlan.DeviceId,
            devicePlan.NewAnchorTargets.ToDictionary(static t => t.Key.Marker, static t => t.JumpTarget, StringComparer.Ordinal),
            devicePlan.OldArtifactHash);
        DeploymentRollbackResult result = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.TerminalImmutable, result.ErrorCode);
    }

    [Fact]
    public async Task RollbackFailsWhenSetAnchorFails()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        runtime.FailNextSet = true;
        DeploymentRollbackResult result = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.RecoveryRequired, result.ErrorCode);
    }

    [Fact]
    public async Task RollbackFailsWhenOldStateProbeFails()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        runtime.ProbeFails = true;
        DeploymentRollbackResult result = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            [runtime],
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.DeploymentProbeFailed, result.ErrorCode);
    }

    [Fact]
    public async Task RollbackFailsWhenRuntimeMissingForDevice()
    {
        (DeploymentPlan plan, DeploymentOperation operation, ScriptedRollbackRuntime runtime) = SeedActivatedNew();
        ScriptedRollbackRuntime wrong = new(
            DeviceId.New(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            runtime.ObservedResourceHash);
        DeploymentRollbackResult result = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            [wrong],
            T0.AddMinutes(1));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.DevicePlanCardinality, result.ErrorCode);
    }

    [Fact]
    public void IncompleteAnchorsClassifyAsIncomplete()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        Assert.Equal(
            DeploymentAnchorSetState.Incomplete,
            DeploymentRecoveryDecision.ClassifyAnchors(
                plan.OldAnchorTargets,
                plan.NewAnchorTargets,
                new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    [Fact]
    public void CodeForMapsRecoveryActions()
    {
        Assert.Equal(DeploymentCodes.RecoveryRequired, DeploymentRecoveryDecision.CodeFor(DeploymentRecoveryAction.RecoveryRequired));
        Assert.Equal(
            DeploymentCodes.WatchdogRollbackDetected,
            DeploymentRecoveryDecision.CodeFor(DeploymentRecoveryAction.RecognizeWatchdogRollback));
        Assert.Null(DeploymentRecoveryDecision.CodeFor(DeploymentRecoveryAction.ControllerRollback));
    }

    private static void AdvanceToCommitted(DeploymentOperation operation)
    {
        operation.EnsureTransition(DeploymentOperationState.Prechecking, T0.AddSeconds(1));
        operation.EnsureTransition(DeploymentOperationState.Staging, T0.AddSeconds(2));
        operation.EnsureTransition(DeploymentOperationState.Staged, T0.AddSeconds(3));
        operation.EnsureTransition(DeploymentOperationState.ArmingWatchdog, T0.AddSeconds(4));
        operation.EnsureTransition(DeploymentOperationState.WatchdogArmed, T0.AddSeconds(5));
        operation.EnsureTransition(DeploymentOperationState.Activating, T0.AddSeconds(6));
        operation.EnsureTransition(DeploymentOperationState.Verifying, T0.AddSeconds(7));
        operation.EnsureTransition(DeploymentOperationState.DisarmingWatchdog, T0.AddSeconds(8));
        operation.EnsureTransition(DeploymentOperationState.Committed, T0.AddSeconds(9));
    }

    private static (DeploymentPlan Plan, DeploymentOperation Operation, ScriptedRollbackRuntime Runtime) SeedActivatedNew()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        operation.EnsureTransition(DeploymentOperationState.Prechecking, T0.AddSeconds(1));
        operation.EnsureTransition(DeploymentOperationState.Staging, T0.AddSeconds(2));
        operation.EnsureTransition(DeploymentOperationState.Staged, T0.AddSeconds(3));
        operation.EnsureTransition(DeploymentOperationState.ArmingWatchdog, T0.AddSeconds(4));
        operation.EnsureTransition(DeploymentOperationState.WatchdogArmed, T0.AddSeconds(5));
        operation.EnsureTransition(DeploymentOperationState.Activating, T0.AddSeconds(6));

        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        Dictionary<string, string> jumps = devicePlan.NewAnchorTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);
        ScriptedRollbackRuntime runtime = new(devicePlan.DeviceId, jumps, devicePlan.OldArtifactHash);
        return (plan, operation, runtime);
    }

    private sealed class ScriptedRollbackRuntime : IDeploymentRollbackDeviceRuntime
    {
        public ScriptedRollbackRuntime(
            DeviceId deviceId,
            Dictionary<string, string> jumps,
            Hash256 oldArtifactHash)
        {
            DeviceId = deviceId;
            Jumps = jumps;
            ObservedResourceHash = oldArtifactHash;
        }

        public DeviceId DeviceId { get; }

        public Dictionary<string, string> Jumps { get; }

        public Hash256 ObservedResourceHash { get; set; }

        public bool FreshOpened { get; private set; }

        public IReadOnlyList<string> SchedulerNames { get; set; } = [];

        public IReadOnlyDictionary<string, bool> SchedulerDisabled { get; set; } =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        public bool FailNextSet { get; set; }

        public bool ProbeFails { get; set; }

        public Task<IReadOnlyDictionary<string, string>> ReadAnchorJumpsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyDictionary<string, string>)new Dictionary<string, string>(Jumps, StringComparer.Ordinal));

        public Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
            AnchorTargetWrite write,
            CancellationToken cancellationToken = default)
        {
            if (FailNextSet)
            {
                FailNextSet = false;
                return Task.FromResult(new DeploymentWriteExecutionResult
                {
                    Succeeded = false,
                    Path = "/ip/firewall/filter/set",
                    SentAttributes = [],
                    ReadBack = new Dictionary<string, string>(StringComparer.Ordinal),
                    Error = "set-failed",
                });
            }

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
        {
            FreshOpened = true;
            return Task.FromResult<IDeploymentFreshSessionFactory>(new FakeFreshFactory());
        }

        public Task<RouterPingResult> ProbeAsync(DeploymentProbe probe, CancellationToken cancellationToken = default)
            => Task.FromResult(new RouterPingResult
            {
                Outcome = ProbeFails ? RouterPingOutcome.Fail : RouterPingOutcome.Pass,
                Sent = 3,
                Received = ProbeFails ? 0 : 3,
            });

        public Task DisarmAndCleanupWatchdogAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<(IReadOnlyList<string> SchedulerNames, IReadOnlyDictionary<string, bool> SchedulerDisabled)>
            ReadWatchdogSchedulerFactsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((SchedulerNames, SchedulerDisabled));
    }

    private sealed class FakeFreshFactory : IDeploymentFreshSessionFactory
    {
        public Task<IRouterOsDeploymentSession> OpenFreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IRouterOsDeploymentSession>(new FakeSession());
    }

    private sealed class FakeSession : IRouterOsDeploymentSession
    {
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
            => Task.FromResult(new RouterPingResult { Outcome = RouterPingOutcome.Pass, Sent = 3, Received = 3 });
    }
}
