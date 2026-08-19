using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>
/// Living Spec matrix for Issue Set M5-08 AC 1–11 (Onboarding Spec §44–§46).
/// </summary>
public sealed class OnboardingRollbackLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 19, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ac1EnabledAnchorsAreDisabledFirst()
    {
        (_, _, _, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session, OnboardingRollbackResult result) =
            await RollbackEnabledAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        List<string> timeline = [.. result.Timeline];
        int firstDisable = timeline.FindIndex(static t => t.StartsWith("disable:", StringComparison.Ordinal));
        int firstRemove = timeline.FindIndex(static t => t.StartsWith("remove-anchor:", StringComparison.Ordinal));
        Assert.True(firstDisable >= 0 && firstRemove > firstDisable);
        Assert.False(session.HasEnabledBootstrapAnchors);
    }

    [Fact]
    public async Task Ac2ManagementAccessIsCheckedAfterDisabling()
    {
        (_, _, _, _, OnboardingRollbackResult result) = await RollbackEnabledAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        List<string> timeline = [.. result.Timeline];
        int lastDisable = timeline.FindLastIndex(static t => t.StartsWith("disable:", StringComparison.Ordinal));
        int reconnect = timeline.FindIndex(static t => t.StartsWith("reconnect:", StringComparison.Ordinal));
        int firstRemove = timeline.FindIndex(static t => t.StartsWith("remove-anchor:", StringComparison.Ordinal));
        Assert.True(lastDisable >= 0 && reconnect > lastDisable && firstRemove > reconnect);
    }

    [Fact]
    public async Task Ac3OnlyExactOperationResourcesAreRemoved()
    {
        (_, _, _, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session, OnboardingRollbackResult result) =
            await RollbackEnabledAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Contains("user-input", session.UserComments);
        Assert.Contains("user-forward", session.UserComments);
        Assert.Contains("user-output", session.UserComments);
        Assert.False(session.HasBootstrapRoots);
        Assert.DoesNotContain(
            await session.PrintFilterAsync(),
            static r => r.Comment is not null && r.Comment.StartsWith("mfc:anchor:v1:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac4BootstrapRootsAreRemovedAfterAnchorReferences()
    {
        (_, _, _, _, OnboardingRollbackResult result) = await RollbackEnabledAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        List<string> timeline = [.. result.Timeline];
        int lastAnchor = timeline.FindLastIndex(static t => t.StartsWith("remove-anchor:", StringComparison.Ordinal));
        int firstRoot = timeline.FindIndex(static t => t.StartsWith("remove-root:", StringComparison.Ordinal));
        Assert.True(lastAnchor >= 0 && firstRoot > lastAnchor);
    }

    [Fact]
    public async Task Ac5WatchdogResidueCleanupIsIdempotent()
    {
        (Node node, OnboardingPlan plan, OnboardingOperation operation, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session, OnboardingRollbackResult first) =
            await RollbackEnabledAsync();
        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.True(first.WatchdogsCleaned);
        Assert.False(session.HasWatchdogResidue);
        OnboardingRollbackResult second = await RollbackOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0.AddMinutes(1));
        Assert.True(second.Succeeded, second.ErrorCode);
        Assert.Equal(OnboardingOperationState.RolledBack, second.State);
        Assert.False(session.HasWatchdogResidue);
    }

    [Fact]
    public async Task Ac6NonterminalOperationIsRolledBackAfterRestart()
    {
        (Node node, OnboardingPlan plan, OnboardingOperation operation, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session) =
            SeedEnabled(OnboardingOperationState.EnablingAnchors);
        OnboardingRecoveryResult recovered = await RecoverOnboardingUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0);
        Assert.Equal(OnboardingRecoveryAction.ControllerRollback, recovered.Action);
        Assert.Equal(OnboardingOperationState.RolledBack, recovered.State);
        Assert.True(recovered.NodeUnmanaged);
        Assert.False(session.HasEnabledBootstrapAnchors);
        Assert.False(session.HasWatchdogResidue);
    }

    [Fact]
    public async Task Ac7UnexpectedAnchorTargetRequiresRecovery()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        operation.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        operation.EnsureTransition(OnboardingOperationState.StagingBootstrapRoots, T0.AddSeconds(2));
        operation.EnsureTransition(OnboardingOperationState.StagingDisabledAnchors, T0.AddSeconds(3));
        operation.EnsureTransition(OnboardingOperationState.ArmingWatchdogs, T0.AddSeconds(4));
        operation.EnsureTransition(OnboardingOperationState.EnablingAnchors, T0.AddSeconds(5));
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        session.SeedExactAnchor(
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            disabled: false,
            jumpTarget: "forward");
        OnboardingRollbackResult result = await RollbackOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingCodes.UnexpectedAnchorTarget, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.RecoveryRequired, result.State);
        Assert.Equal(ManagementState.RecoveryRequired, node.ManagementState);
        Assert.Contains(await session.PrintFilterAsync(), static r => r.Comment == "mfc:anchor:v1:4:i");
    }

    [Fact]
    public void Ac8AutomaticAdoptionIsAbsent()
    {
        Assert.DoesNotContain(
            typeof(OnboardingRecoveryDecision).GetMethods(),
            static m => m.Name.Contains("Adopt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(RecoverOnboardingUseCase).GetMethods(),
            static m => m.Name.Contains("Adopt", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            OnboardingRecoveryAction.RecoveryRequired,
            OnboardingRecoveryDecision.Decide(
                OnboardingAnchorSetState.UnexpectedTarget,
                OnboardingWatchdogPresence.Active,
                committed: false));
        Assert.Equal(
            OnboardingRecoveryAction.RecoveryRequired,
            OnboardingRecoveryDecision.Decide(
                OnboardingAnchorSetState.UnexpectedTarget,
                OnboardingWatchdogPresence.AbsentOrDisabled,
                committed: true));
    }

    [Fact]
    public async Task Ac9PartialVrrpOnboardingRollsBackAllMembers()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        AdvanceTo(operation, OnboardingOperationState.EnablingAnchors);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession a =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(first.Id);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession b =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(second.Id);
        SeedDevice(plan, operation, a, enabled: true, watchdogActive: true);
        SeedDevice(plan, operation, b, enabled: false, watchdogActive: true);
        OnboardingRollbackResult result = await RollbackOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [a, b],
            T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(2, result.Timeline.Count(static t => t.StartsWith("cleanup-watchdog:", StringComparison.Ordinal)));
        Assert.False(a.HasEnabledBootstrapAnchors);
        Assert.False(b.HasEnabledBootstrapAnchors);
        Assert.False(a.HasWatchdogResidue);
        Assert.False(b.HasWatchdogResidue);
        Assert.Equal(ManagementState.Unmanaged, node.ManagementState);
    }

    [Fact]
    public async Task Ac10FailedOnboardingLeavesNoEnabledAnchors()
    {
        (_, _, _, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session, OnboardingRollbackResult result) =
            await RollbackEnabledAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.False(result.RemainingEnabledAnchors);
        Assert.False(session.HasEnabledBootstrapAnchors);
        Assert.Equal(OnboardingOperationState.RolledBack, result.State);
    }

    [Theory]
    [MemberData(nameof(RecoveryTableRows))]
    public void Ac11RecoveryDecisionTableIsComplete(
        OnboardingAnchorSetState anchors,
        OnboardingWatchdogPresence watchdog,
        bool committed,
        OnboardingRecoveryAction expected)
    {
        Assert.Equal(expected, OnboardingRecoveryDecision.Decide(anchors, watchdog, committed));
    }

    [Fact]
    public void ClassifyAnchorsCoversCommittedAndMixedRows()
    {
        AnchorKey input = AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input);
        AnchorKey output = AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Output);
        AnchorKey[] required = [input, output];
        ActualFilterRule enabled = Rule(input, disabled: false);
        ActualFilterRule disabled = Rule(output, disabled: true);
        Assert.Equal(
            OnboardingAnchorSetState.Absent,
            OnboardingRecoveryDecision.ClassifyAnchors(required, [], committed: false));
        Assert.Equal(
            OnboardingAnchorSetState.MixedEnablement,
            OnboardingRecoveryDecision.ClassifyAnchors(required, [enabled], committed: false));
        Assert.Equal(
            OnboardingAnchorSetState.MixedEnablement,
            OnboardingRecoveryDecision.ClassifyAnchors(required, [enabled, disabled], committed: false));
        Assert.Equal(
            OnboardingAnchorSetState.AllEnabledBootstrap,
            OnboardingRecoveryDecision.ClassifyAnchors(required, [Rule(input, false), Rule(output, false)], committed: false));
        Assert.Equal(
            OnboardingAnchorSetState.AllDisabledBootstrap,
            OnboardingRecoveryDecision.ClassifyAnchors(required, [Rule(input, true), Rule(output, true)], committed: false));
        Assert.Equal(
            OnboardingAnchorSetState.CommittedMissing,
            OnboardingRecoveryDecision.ClassifyAnchors(required, [Rule(input, false)], committed: true));
        Assert.Equal(
            OnboardingAnchorSetState.CommittedDisabled,
            OnboardingRecoveryDecision.ClassifyAnchors(required, [Rule(input, true), Rule(output, false)], committed: true));
        Assert.Equal(
            OnboardingAnchorSetState.UnexpectedTarget,
            OnboardingRecoveryDecision.ClassifyAnchors(
                [input],
                [ActualFilterRule.Create(IpAddressFamily.IPv4, "input", 0, "jump", jumpTarget: "fwd", comment: input.Marker)],
                committed: false));
        Assert.Equal(
            OnboardingWatchdogPresence.Active,
            OnboardingRecoveryDecision.ClassifyWatchdog(new OnboardingSystemNameFacts
            {
                ScriptNames = [],
                SchedulerNames = ["mfc-ob-d-0123456789abcdef"],
                SchedulerDisabled = new Dictionary<string, bool>(StringComparer.Ordinal),
            }));
        Assert.Equal(
            OnboardingWatchdogPresence.AbsentOrDisabled,
            OnboardingRecoveryDecision.ClassifyWatchdog(new OnboardingSystemNameFacts
            {
                ScriptNames = [],
                SchedulerNames = ["mfc-ob-d-0123456789abcdef"],
                SchedulerDisabled = new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["mfc-ob-d-0123456789abcdef"] = true,
                },
            }));
    }

    [Fact]
    public async Task ReconnectFailureDuringRollbackRequiresRecovery()
    {
        (Node node, OnboardingPlan plan, OnboardingOperation operation, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session) =
            SeedEnabled(OnboardingOperationState.RollbackPending);
        session.FailReconnect = true;
        OnboardingRollbackResult result = await RollbackOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingCodes.OnboardingManagementReconnectFailed, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.RecoveryRequired, result.State);
    }

    [Fact]
    public async Task CommittedKeepManagedCleansDisabledWatchdogResidue()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        AdvanceTo(operation, OnboardingOperationState.Committed);
        foreach (Device member in node.Devices)
        {
            member.SetManagementState(ManagementState.Managed);
        }

        node.SetManagementState(ManagementState.Managed);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        SeedDevice(plan, operation, session, enabled: true, watchdogActive: false);
        OnboardingRecoveryResult recovered = await RecoverOnboardingUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0);
        Assert.Equal(OnboardingRecoveryAction.KeepManaged, recovered.Action);
        Assert.Equal(OnboardingOperationState.Committed, recovered.State);
        Assert.True(recovered.NodeManaged);
        Assert.False(session.HasWatchdogResidue);
    }

    [Fact]
    public async Task CommittedMissingAnchorIsCriticalDriftWithoutAdoption()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        AdvanceTo(operation, OnboardingOperationState.Committed);
        foreach (Device member in node.Devices)
        {
            member.SetManagementState(ManagementState.Managed);
        }

        node.SetManagementState(ManagementState.Managed);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        OnboardingRecoveryResult recovered = await RecoverOnboardingUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0);
        Assert.Equal(OnboardingRecoveryAction.CriticalDrift, recovered.Action);
        Assert.Equal(OnboardingCodes.OnboardingCriticalDrift, recovered.ErrorCode);
        Assert.Equal(OnboardingOperationState.Committed, recovered.State);
        Assert.Equal(ManagementState.RecoveryRequired, node.ManagementState);
    }

    [Fact]
    public async Task AbsentNonterminalCleansUpToRolledBack()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        session.SeedWatchdog(operation.Id, device.Id, disabled: true);
        OnboardingRecoveryResult recovered = await RecoverOnboardingUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0);
        Assert.Equal(OnboardingRecoveryAction.CleanupRolledBack, recovered.Action);
        Assert.Equal(OnboardingOperationState.RolledBack, recovered.State);
        Assert.True(recovered.NodeUnmanaged);
        Assert.False(session.HasWatchdogResidue);
    }

    [Fact]
    public void ReverseEnableOrderIsInputThenOutputThenForward()
    {
        AnchorKey[] keys =
        [
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Forward),
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Output),
        ];
        Assert.Equal(
            ["mfc:anchor:v1:4:i", "mfc:anchor:v1:4:o", "mfc:anchor:v1:4:f"],
            OnboardingEnableOrder.Reverse(keys).Select(static k => k.Marker).ToArray());
    }

    public static TheoryData<OnboardingAnchorSetState, OnboardingWatchdogPresence, bool, OnboardingRecoveryAction> RecoveryTableRows
        => new()
        {
            { OnboardingAnchorSetState.Absent, OnboardingWatchdogPresence.Active, false, OnboardingRecoveryAction.CleanupRolledBack },
            { OnboardingAnchorSetState.Absent, OnboardingWatchdogPresence.AbsentOrDisabled, false, OnboardingRecoveryAction.CleanupRolledBack },
            { OnboardingAnchorSetState.AllDisabledBootstrap, OnboardingWatchdogPresence.Active, false, OnboardingRecoveryAction.CleanupRolledBack },
            { OnboardingAnchorSetState.AllEnabledBootstrap, OnboardingWatchdogPresence.Active, false, OnboardingRecoveryAction.ControllerRollback },
            { OnboardingAnchorSetState.AllEnabledBootstrap, OnboardingWatchdogPresence.AbsentOrDisabled, false, OnboardingRecoveryAction.ControllerRollback },
            { OnboardingAnchorSetState.MixedEnablement, OnboardingWatchdogPresence.Active, false, OnboardingRecoveryAction.ControllerRollback },
            { OnboardingAnchorSetState.UnexpectedTarget, OnboardingWatchdogPresence.Active, false, OnboardingRecoveryAction.RecoveryRequired },
            { OnboardingAnchorSetState.AllEnabledBootstrap, OnboardingWatchdogPresence.AbsentOrDisabled, true, OnboardingRecoveryAction.KeepManaged },
            { OnboardingAnchorSetState.CommittedMissing, OnboardingWatchdogPresence.Active, true, OnboardingRecoveryAction.CriticalDrift },
            { OnboardingAnchorSetState.CommittedDisabled, OnboardingWatchdogPresence.AbsentOrDisabled, true, OnboardingRecoveryAction.CriticalDrift },
            { OnboardingAnchorSetState.Absent, OnboardingWatchdogPresence.Active, true, OnboardingRecoveryAction.CriticalDrift },
        };

    private static async Task<(Node Node, OnboardingPlan Plan, OnboardingOperation Operation, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession Session, OnboardingRollbackResult Result)> RollbackEnabledAsync()
    {
        (Node node, OnboardingPlan plan, OnboardingOperation operation, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session) =
            SeedEnabled(OnboardingOperationState.EnablingAnchors);
        OnboardingRollbackResult result = await RollbackOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0);
        return (node, plan, operation, session, result);
    }

    private static (Node Node, OnboardingPlan Plan, OnboardingOperation Operation, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession Session) SeedEnabled(
        OnboardingOperationState state)
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        AdvanceTo(operation, state);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        SeedDevice(plan, operation, session, enabled: true, watchdogActive: true);
        return (node, plan, operation, session);
    }

    private static void SeedDevice(
        OnboardingPlan plan,
        OnboardingOperation operation,
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session,
        bool enabled,
        bool watchdogActive)
    {
        DeviceOnboardingPlan devicePlan = plan.DevicePlans.Single(p => p.DeviceId == session.DeviceId);
        foreach (AnchorKey key in devicePlan.RequiredAnchorSet)
        {
            session.SeedBootstrapReturn(key);
            session.SeedExactAnchor(key, disabled: !enabled);
        }

        session.SeedWatchdog(operation.Id, session.DeviceId, disabled: !watchdogActive);
    }

    private static void AdvanceTo(OnboardingOperation operation, OnboardingOperationState target)
    {
        DateTimeOffset now = T0.AddSeconds(1);
        if (target == OnboardingOperationState.RollbackPending)
        {
            operation.EnsureTransition(OnboardingOperationState.Prechecking, now);
            operation.EnsureTransition(OnboardingOperationState.StagingBootstrapRoots, now.AddSeconds(1));
            operation.EnsureTransition(OnboardingOperationState.RollbackPending, now.AddSeconds(2));
            return;
        }

        OnboardingOperationState[] path =
        [
            OnboardingOperationState.Prechecking,
            OnboardingOperationState.StagingBootstrapRoots,
            OnboardingOperationState.StagingDisabledAnchors,
            OnboardingOperationState.ArmingWatchdogs,
            OnboardingOperationState.EnablingAnchors,
            OnboardingOperationState.Verifying,
            OnboardingOperationState.DisarmingWatchdogs,
            OnboardingOperationState.Committed,
        ];
        foreach (OnboardingOperationState next in path)
        {
            if (operation.State == target)
            {
                return;
            }

            operation.EnsureTransition(next, now);
            now = now.AddSeconds(1);
        }
    }

    private static ActualFilterRule Rule(AnchorKey key, bool disabled)
        => ActualFilterRule.Create(
            key.Family,
            key.Chain switch
            {
                FilterBuiltInContext.Input => "input",
                FilterBuiltInContext.Forward => "forward",
                FilterBuiltInContext.Output => "output",
                _ => "input",
            },
            0,
            "jump",
            disabled,
            jumpTarget: BootstrapArtifact.RootChainName(key.Family, key.Chain),
            comment: key.Marker);
}
