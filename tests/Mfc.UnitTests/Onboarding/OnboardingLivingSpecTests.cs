using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>
/// Living Spec matrix for Issue Set M5-01 AC 1–10 (Onboarding Spec §4–§5, §18, §23, §25–§26, §48, §52, §54).
/// </summary>
public sealed class OnboardingLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ac1AllOnboardingStatesAreImplementedAndHappyPathTransitions()
    {
        Assert.Equal(14, Enum.GetValues<OnboardingOperationState>().Length);
        Node node = OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        Assert.Equal(OnboardingOperationState.Created, operation.State);
        Assert.Equal(1ul, operation.RowVersion);

        OnboardingOperationState[] happy =
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
        DateTimeOffset t = T0;
        foreach (OnboardingOperationState next in happy)
        {
            t = t.AddSeconds(1);
            Assert.True(OnboardingOperationGate.EvaluateTransition(operation, next).Allowed);
            operation.EnsureTransition(next, t);
        }

        Assert.Equal(OnboardingOperationState.Committed, operation.State);
        Assert.NotNull(operation.StartedAtUtc);
        Assert.NotNull(operation.CompletedAtUtc);
        Assert.Equal(9ul, operation.RowVersion);
        Assert.False(operation.IsNonterminal);
        Assert.True(OnboardingOperation.IsTerminalState(OnboardingOperationState.Committed));
        Assert.True(OnboardingOperation.IsTerminalState(OnboardingOperationState.RolledBack));
        Assert.True(OnboardingOperation.IsTerminalState(OnboardingOperationState.Blocked));
        Assert.True(OnboardingOperation.IsTerminalState(OnboardingOperationState.RecoveryRequired));
    }

    [Fact]
    public void Ac2NodeAndDeviceHaveIndependentManagementStates()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        Assert.Equal(ManagementState.Unmanaged, node.ManagementState);
        Assert.Equal(ManagementState.Unmanaged, device.ManagementState);
        node.Activate();
        Assert.Equal(NodeStatus.Active, node.Status);
        Assert.NotEqual((int)node.Status, (int)node.ManagementState);

        Assert.Throws<DomainInvariantException>(() => node.SetManagementState(ManagementState.Managed));

        device.SetManagementState(ManagementState.Managed);
        node.SetManagementState(ManagementState.Managed);
        Assert.Equal(ManagementState.Managed, node.ManagementState);
        Assert.Equal(ManagementState.Managed, device.ManagementState);
        Assert.Equal(NodeStatus.Active, node.Status);
        Assert.Throws<DomainInvariantException>(() => node.AddDevice(
            NonEmptyName.Create("x"),
            ManagementEndpoint.Create("10.9.9.9"),
            DeviceRole.Router));

        node.SetManagementState(ManagementState.RecoveryRequired);
        device.SetManagementState(ManagementState.RecoveryRequired);
        Assert.Equal(ManagementState.RecoveryRequired, node.ManagementState);
    }

    [Fact]
    public void Ac3VrrpOnboardingTargetsTheWholeNode()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        Assert.Equal(2, plan.DevicePlans.Count);
        Assert.Contains(plan.DevicePlans, p => p.DeviceId == first.Id);
        Assert.Contains(plan.DevicePlans, p => p.DeviceId == second.Id);

        DeviceOnboardingPlan onlyFirst = OnboardingTestFactory.DevicePlan(first.Id, NodeKind.Vrrp);
        DomainInvariantException partial = Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Create(node, OnboardingTestFactory.H("m"), OnboardingTestFactory.H("t"), [onlyFirst], UserId.New(), T0));
        Assert.Contains("every Node member", partial.Message, StringComparison.Ordinal);

        Node router = OnboardingTestFactory.RouterWithDevice(out Device routerDevice);
        DomainInvariantException extra = Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Create(
                router,
                OnboardingTestFactory.H("m"),
                OnboardingTestFactory.H("t"),
                [
                    OnboardingTestFactory.DevicePlan(routerDevice.Id, NodeKind.Router),
                    OnboardingTestFactory.DevicePlan(second.Id, NodeKind.Router),
                ],
                UserId.New(),
                T0));
        Assert.Contains("exactly one device plan", extra.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4PlanIsImmutableWithBoundedLifetime()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        List<DeviceOnboardingPlan> input = [OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router)];
        OnboardingPlan plan = OnboardingPlan.Create(
            node,
            OnboardingTestFactory.H("m"),
            OnboardingTestFactory.H("t"),
            input,
            UserId.New(),
            T0);
        Assert.Equal(T0.Add(OnboardingCodes.DefaultPlanLifetime), plan.ExpiresAtUtc);
        Assert.False(plan.IsExpired(T0.AddMinutes(29)));
        Assert.True(plan.IsExpired(T0.AddMinutes(30)));
        Assert.True(plan.DevicePlans is DeviceOnboardingPlan[]);
        input.Clear();
        Assert.Single(plan.DevicePlans);

        Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Create(
                node,
                OnboardingTestFactory.H("m"),
                OnboardingTestFactory.H("t"),
                [OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router)],
                UserId.New(),
                T0,
                T0.AddMinutes(-1)));

        OnboardingPlan clone = OnboardingPlan.Reconstitute(
            plan.Id,
            plan.NodeId,
            plan.NodeMembershipHash,
            plan.TopologyProjectionHash,
            plan.DevicePlans,
            plan.CreatedBy,
            plan.CreatedAtUtc,
            plan.ExpiresAtUtc,
            plan.PlanHash);
        Assert.Equal(plan.PlanHash.ToString(), clone.PlanHash.ToString());
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Reconstitute(
                plan.Id,
                plan.NodeId,
                OnboardingTestFactory.H("other-membership"),
                plan.TopologyProjectionHash,
                plan.DevicePlans,
                plan.CreatedBy,
                plan.CreatedAtUtc,
                plan.ExpiresAtUtc,
                plan.PlanHash));
    }

    [Fact]
    public void Ac5PlanHashCoversSpec25DependenciesAndExcludesVrrpRoleAndActiveWan()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        Assert.Equal(OnboardingPlanHasher.Compute(plan).ToString(), plan.PlanHash.ToString());
        Assert.Equal(OnboardingCodes.PlanHashPrefix, "mfc.onboarding.plan.v1");

        OnboardingPlan otherCfg = OnboardingPlan.Create(
            node,
            plan.NodeMembershipHash,
            plan.TopologyProjectionHash,
            [OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router, configurationHash: OnboardingTestFactory.H("cfg-2"))],
            plan.CreatedBy,
            T0,
            plan.ExpiresAtUtc);
        Assert.NotEqual(plan.PlanHash.ToString(), otherCfg.PlanHash.ToString());

        OnboardingPlan sameAgain = OnboardingPlan.Create(
            node,
            plan.NodeMembershipHash,
            plan.TopologyProjectionHash,
            [OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router)],
            plan.CreatedBy,
            T0,
            plan.ExpiresAtUtc);
        Assert.Equal(
            OnboardingPlanHasher.Compute(
                node.Id,
                plan.NodeMembershipHash,
                plan.TopologyProjectionHash,
                sameAgain.DevicePlans,
                plan.CreatedBy,
                T0,
                plan.ExpiresAtUtc).ToString(),
            sameAgain.PlanHash.ToString());

        System.Reflection.ParameterInfo[] parameters = typeof(OnboardingPlanHasher)
            .GetMethod(nameof(OnboardingPlanHasher.Compute), [typeof(OnboardingPlan)])!
            .GetParameters();
        Assert.DoesNotContain(parameters, static p => p.Name is not null && p.Name.Contains("Role", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parameters, static p => p.Name is not null && p.Name.Contains("Wan", StringComparison.OrdinalIgnoreCase));
        Assert.Null(typeof(DeviceOnboardingPlan).GetProperty("VrrpRole"));
        Assert.Null(typeof(DeviceOnboardingPlan).GetProperty("ActiveWan"));
        Assert.Null(typeof(OnboardingPlan).GetProperty("VrrpRole"));
        Assert.Null(typeof(OnboardingPlan).GetProperty("ActiveWanName"));
    }

    [Fact]
    public void Ac6OneNonterminalOnboardingPerNode()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation first = OnboardingOperation.Create(plan, UserId.New(), T0);
        Assert.True(OnboardingOperationGate.EvaluateCreate(plan, [], T0).Allowed);
        OnboardingGateEvaluation blocked = OnboardingOperationGate.EvaluateCreate(plan, [first], T0);
        Assert.False(blocked.Allowed);
        Assert.Equal(OnboardingCodes.NonterminalExists, blocked.ErrorCode);

        first.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        first.EnsureTransition(OnboardingOperationState.Blocked, T0.AddSeconds(2), OnboardingCodes.NamespaceCollision);
        Assert.True(OnboardingOperation.IsTerminalState(first.State));
        Assert.True(OnboardingOperationGate.EvaluateCreate(plan, [first], T0.AddSeconds(3)).Allowed);
    }

    [Fact]
    public void Ac7WriteAheadStepJournalIsImplemented()
    {
        Assert.Equal(4, Enum.GetValues<OnboardingStepState>().Length);
        OnboardingStep step = OnboardingStep.Create(
            OnboardingOperationId.New(),
            Mfc.Domain.Inventory.Primitives.DeviceId.New(),
            sequence: 1,
            OnboardingStepKind.CreateDisabledAnchor,
            OnboardingTestFactory.H("before"),
            OnboardingTestFactory.H("after"),
            T0);
        Assert.Equal(OnboardingStepState.IntentRecorded, step.State);
        step.RecordEffectSent(T0.AddSeconds(1));
        Assert.Equal(OnboardingStepState.EffectSent, step.State);
        step.MarkVerified(T0.AddSeconds(2));
        Assert.Equal(OnboardingStepState.Verified, step.State);
        Assert.True(step.IsTerminal);
        Assert.Throws<DomainInvariantException>(() => step.MarkFailed(T0.AddSeconds(3)));

        OnboardingStep failedFromIntent = OnboardingStep.Create(
            OnboardingOperationId.New(),
            Mfc.Domain.Inventory.Primitives.DeviceId.New(),
            1,
            OnboardingStepKind.CreateBootstrapRoot,
            OnboardingTestFactory.H("b"),
            OnboardingTestFactory.H("a"),
            T0);
        failedFromIntent.MarkFailed(T0.AddSeconds(1));
        Assert.Equal(OnboardingStepState.Failed, failedFromIntent.State);

        OnboardingStep failedFromEffect = OnboardingStep.Create(
            OnboardingOperationId.New(),
            Mfc.Domain.Inventory.Primitives.DeviceId.New(),
            2,
            OnboardingStepKind.EnableAnchor,
            OnboardingTestFactory.H("b"),
            OnboardingTestFactory.H("a"),
            T0);
        failedFromEffect.RecordEffectSent(T0.AddSeconds(1));
        failedFromEffect.MarkFailed(T0.AddSeconds(2));
        Assert.Equal(OnboardingStepState.Failed, failedFromEffect.State);
        Assert.Throws<DomainInvariantException>(() => failedFromEffect.MarkVerified(T0.AddSeconds(3)));
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingStep.Create(
                OnboardingOperationId.New(),
                Mfc.Domain.Inventory.Primitives.DeviceId.New(),
                0,
                OnboardingStepKind.EnableAnchor,
                OnboardingTestFactory.H("b"),
                OnboardingTestFactory.H("a"),
                T0));
    }

    [Fact]
    public void Ac8CompletedOperationIsImmutable()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingOperation operation = OnboardingOperation.Create(OnboardingTestFactory.PlanFor(node, T0), UserId.New(), T0);
        operation.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        operation.EnsureTransition(OnboardingOperationState.Blocked, T0.AddSeconds(2), OnboardingCodes.ApiSslInvalid);
        Assert.Equal(OnboardingCodes.ApiSslInvalid, operation.ErrorCode);
        DomainInvariantException frozen = Assert.Throws<DomainInvariantException>(() =>
            operation.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(3)));
        Assert.Contains("Terminal", frozen.Message, StringComparison.Ordinal);
        OnboardingGateEvaluation gate = OnboardingOperationGate.EvaluateTransition(
            operation,
            OnboardingOperationState.RolledBack);
        Assert.Equal(OnboardingCodes.InvalidTransition, gate.ErrorCode);
    }

    [Fact]
    public void Ac9StateTransitionsAreRowVersioned()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingOperation operation = OnboardingOperation.Create(OnboardingTestFactory.PlanFor(node, T0), UserId.New(), T0);
        ulong before = operation.RowVersion;
        operation.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        Assert.Equal(before + 1, operation.RowVersion);
        operation.EnsureTransition(OnboardingOperationState.StagingBootstrapRoots, T0.AddSeconds(2));
        operation.EnsureTransition(OnboardingOperationState.RollbackPending, T0.AddSeconds(3));
        operation.EnsureTransition(OnboardingOperationState.RollingBack, T0.AddSeconds(4));
        operation.EnsureTransition(OnboardingOperationState.RolledBack, T0.AddSeconds(5));
        Assert.Equal(6ul, operation.RowVersion);
        Assert.NotNull(operation.CompletedAtUtc);
    }

    [Fact]
    public void Ac10InvalidTransitionIsRejected()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingOperation operation = OnboardingOperation.Create(OnboardingTestFactory.PlanFor(node, T0), UserId.New(), T0);
        OnboardingGateEvaluation skip = OnboardingOperationGate.EvaluateTransition(
            operation,
            OnboardingOperationState.Committed);
        Assert.False(skip.Allowed);
        Assert.Equal(OnboardingCodes.InvalidTransition, skip.ErrorCode);
        Assert.Throws<DomainInvariantException>(() =>
            operation.EnsureTransition(OnboardingOperationState.EnablingAnchors, T0.AddSeconds(1)));
        Assert.Equal(OnboardingOperationState.Created, operation.State);

        operation.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        Assert.True(OnboardingOperationGate.EvaluateTransition(operation, OnboardingOperationState.Blocked).Allowed);
        Assert.False(OnboardingOperationGate.EvaluateTransition(operation, OnboardingOperationState.RollbackPending).Allowed);
    }
}
