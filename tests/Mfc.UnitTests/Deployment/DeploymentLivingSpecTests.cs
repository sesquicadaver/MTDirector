using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-01 AC 1–12 (Safe Deployment Spec §9–§16).
/// </summary>
public sealed class DeploymentLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 19, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ac1DeploymentTargetIsASingleNode()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        Assert.Equal(node.Id, plan.NodeId);
        DeploymentOperation operation = DeploymentOperation.Create(plan, node, UserId.New(), T0);
        Assert.Equal(node.Id, operation.NodeId);
        Assert.DoesNotContain(
            typeof(DeploymentPlan).GetProperties(),
            static p => p.Name.Contains("Campaign", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Site", StringComparison.OrdinalIgnoreCase) && p.Name != "SiteId");
    }

    [Fact]
    public void Ac2CampaignStateIsAbsent()
    {
        Assert.DoesNotContain(
            typeof(DeploymentPlan).Assembly.GetTypes(),
            static t => t.Namespace == "Mfc.Domain.Deployment"
                        && t.Name.Contains("Campaign", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(DeploymentOperation).GetProperties(),
            static p => p.Name.Contains("Campaign", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(DeploymentCodes.CampaignForbidden, "DEPLOYMENT_CAMPAIGN_FORBIDDEN");
    }

    [Fact]
    public void Ac3PlanContainsOldNewArtifactsAndAnchorTargets()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out Device device);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeviceDeploymentPlan devicePlan = plan.DevicePlans.Single(p => p.DeviceId == device.Id);
        Assert.Equal(32, devicePlan.OldArtifactHash.Bytes.Length);
        Assert.Equal(32, devicePlan.NewArtifactHash.Bytes.Length);
        Assert.NotEqual(devicePlan.OldArtifactHash, devicePlan.NewArtifactHash);
        Assert.NotEmpty(devicePlan.OldAnchorTargets);
        Assert.NotEmpty(devicePlan.NewAnchorTargets);
        Assert.Equal(devicePlan.OldAnchorTargets.Count, devicePlan.NewAnchorTargets.Count);
        Assert.All(devicePlan.NewAnchorTargets, static t => Assert.StartsWith("mfc", t.JumpTarget, StringComparison.Ordinal));
    }

    [Fact]
    public void Ac4PlanIsImmutableAndBoundedByExpiry()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        Assert.True(plan.ExpiresAtUtc > plan.CreatedAtUtc);
        Assert.False(plan.IsExpired(T0.AddMinutes(1)));
        Assert.True(plan.IsExpired(plan.ExpiresAtUtc));
        Assert.Throws<DomainInvariantException>(() =>
            DeploymentPlan.Create(
                node,
                DeploymentTestFactory.H("policy"),
                DeploymentTestFactory.H("analysis"),
                DeploymentTestFactory.H("topology"),
                plan.DevicePlans,
                UserId.New(),
                T0,
                T0));
        Assert.DoesNotContain(
            typeof(DeploymentPlan).GetMethods(),
            static m => m.Name.StartsWith("set_", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac5DevicePlansCoverEveryMember()
    {
        Node vrrp = DeploymentTestFactory.VrrpWithMembers(out Device first, out Device second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(vrrp, T0);
        Assert.Equal(2, plan.DevicePlans.Count);
        Assert.Contains(plan.DevicePlans, p => p.DeviceId == first.Id);
        Assert.Contains(plan.DevicePlans, p => p.DeviceId == second.Id);
        Node router = DeploymentTestFactory.RouterWithDevice(out Device only);
        Assert.Throws<DomainInvariantException>(() =>
            DeploymentPlan.Create(
                router,
                DeploymentTestFactory.H("p"),
                DeploymentTestFactory.H("a"),
                DeploymentTestFactory.H("t"),
                [DeploymentTestFactory.DevicePlan(only.Id, NodeKind.Router), DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router)],
                UserId.New(),
                T0));
    }

    [Fact]
    public void Ac6ActivationAndRollbackOrderAreFixed()
    {
        Node vrrp = DeploymentTestFactory.VrrpWithMembers(out Device first, out Device second);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(vrrp, T0);
        Assert.Equal(2, plan.ActivationOrder.Count);
        Assert.Equal(plan.ActivationOrder.Reverse().ToArray(), plan.RollbackOrder.ToArray());
        DeviceId[] expected = new[] { first.Id, second.Id }.OrderBy(static id => id.Value).ToArray();
        Assert.Equal(expected, plan.ActivationOrder.ToArray());
        DeviceDeploymentPlan slice = plan.DevicePlans[0];
        Assert.Equal(slice.AnchorActivationOrder.Reverse().ToArray(), slice.AnchorRollbackOrder.ToArray());
    }

    [Fact]
    public void Ac7DurableNodeLockIsExclusive()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, UserId.New(), T0);
        DeploymentLock held = DeploymentLock.Acquire(node.Id, operation.Id, "instance-a", T0);
        Assert.Equal(operation.Id, held.DeploymentId);
        Assert.False(held.IsExpired(T0.AddMinutes(1)));
        Assert.Throws<DomainInvariantException>(() =>
            DeploymentLock.Acquire(node.Id, DeploymentOperationId.New(), "instance-b", T0, existing: held));
        held.Heartbeat("instance-a", T0.AddMinutes(1));
        Assert.Throws<DomainInvariantException>(() => held.Heartbeat("instance-b", T0.AddMinutes(1)));
        Assert.True(held.IsExpired(T0.AddHours(1)));
        Assert.Throws<DomainInvariantException>(() =>
            DeploymentLock.Acquire(node.Id, operation.Id, "instance-a", T0.AddHours(1), existing: held));
    }

    [Fact]
    public void Ac8WriteAheadStepJournalIsOrdered()
    {
        DeploymentStep step = DeploymentStep.Create(
            DeploymentOperationId.New(),
            DeviceId.New(),
            1,
            DeploymentStepKind.StageFilterChain,
            DeploymentTestFactory.H("before"),
            DeploymentTestFactory.H("after"),
            T0);
        Assert.Equal(DeploymentStepState.IntentRecorded, step.State);
        Assert.Throws<DomainInvariantException>(() => step.MarkVerified(T0.AddSeconds(1)));
        step.RecordEffectSent(T0.AddSeconds(1));
        step.MarkVerified(T0.AddSeconds(2));
        Assert.True(step.IsTerminal);
        Assert.Throws<DomainInvariantException>(() => step.MarkFailed(T0.AddSeconds(3)));
        DeploymentStep failed = DeploymentStep.Create(
            DeploymentOperationId.New(),
            DeviceId.New(),
            2,
            DeploymentStepKind.ActivateAnchor,
            DeploymentTestFactory.H("b"),
            DeploymentTestFactory.H("a"),
            T0);
        failed.MarkFailed(T0.AddSeconds(1), "sanitized");
        Assert.Equal(DeploymentStepState.Failed, failed.State);
        Assert.Equal("sanitized", failed.SanitizedError);
    }

    [Fact]
    public void Ac9InvalidStateTransitionIsRejected()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, UserId.New(), T0);
        Assert.Equal(18, Enum.GetValues<DeploymentOperationState>().Length);
        Assert.Throws<DomainInvariantException>(() =>
            operation.EnsureTransition(DeploymentOperationState.Committed, T0.AddSeconds(1)));
        operation.EnsureTransition(DeploymentOperationState.Prechecking, T0.AddSeconds(1));
        Assert.NotNull(operation.StartedAtUtc);
        Assert.Throws<DomainInvariantException>(() =>
            operation.EnsureTransition(DeploymentOperationState.Committed, T0.AddSeconds(2)));
    }

    [Fact]
    public void Ac10CompletedDeploymentIsImmutable()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out Device device);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation operation = DeploymentOperation.Create(plan, UserId.New(), T0);
        AdvanceHappy(operation);
        DeviceDeployment member = DeviceDeployment.Create(operation.Id, device.Id, T0);
        AdvanceDeviceHappy(member);
        operation.EnsureCommitted([member], T0.AddMinutes(1));
        Assert.Equal(DeploymentOperationState.Committed, operation.State);
        Assert.NotNull(operation.CompletedAtUtc);
        Assert.Throws<DomainInvariantException>(() =>
            operation.EnsureTransition(DeploymentOperationState.RollbackPending, T0.AddMinutes(2)));
        Assert.Throws<DomainInvariantException>(() =>
            member.EnsureTransition(DeviceDeploymentState.RollingBack, T0.AddMinutes(2)));
    }

    [Fact]
    public void Ac11NoChangesIsTerminalWithoutMutationPath()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0, noChanges: true);
        Assert.All(
            plan.DevicePlans,
            static p =>
            {
                Assert.Equal(p.OldArtifactHash, p.NewArtifactHash);
                Assert.Equal(
                    p.OldAnchorTargets.Select(static t => t.JumpTarget),
                    p.NewAnchorTargets.Select(static t => t.JumpTarget));
            });
        DeploymentOperation operation = DeploymentOperation.Create(plan, UserId.New(), T0);
        operation.EnsureTransition(DeploymentOperationState.Prechecking, T0.AddSeconds(1));
        operation.EnsureTransition(DeploymentOperationState.NoChanges, T0.AddSeconds(2));
        Assert.True(operation.IsTerminal);
        Assert.True(DeploymentOperation.IsTerminalState(DeploymentOperationState.NoChanges));
        Assert.Throws<DomainInvariantException>(() =>
            operation.EnsureTransition(DeploymentOperationState.Staging, T0.AddSeconds(3)));
    }

    [Fact]
    public void Ac12PlanHashIncludesNormativePreconditions()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        Assert.Equal(plan.PlanHash, DeploymentPlanHasher.Compute(plan));
        DeploymentPlan reconstituted = DeploymentPlan.Reconstitute(
            plan.Id,
            plan.NodeId,
            plan.LogicalPolicyHash,
            plan.AnalysisBundleHash,
            plan.TopologyProjectionHash,
            plan.DevicePlans,
            plan.ActivationOrder,
            plan.RollbackOrder,
            plan.CreatedBy,
            plan.CreatedAtUtc,
            plan.ExpiresAtUtc,
            plan.PlanHash);
        Assert.Equal(plan.PlanHash, reconstituted.PlanHash);
        Assert.Throws<DomainInvariantException>(() =>
            DeploymentPlan.Reconstitute(
                plan.Id,
                plan.NodeId,
                plan.LogicalPolicyHash,
                plan.AnalysisBundleHash,
                plan.TopologyProjectionHash,
                plan.DevicePlans,
                plan.ActivationOrder,
                plan.RollbackOrder,
                plan.CreatedBy,
                plan.CreatedAtUtc,
                plan.ExpiresAtUtc,
                DeploymentTestFactory.H("tampered")));
        Hash256 shifted = DeploymentPlanHasher.Hash(
            plan.NodeId,
            DeploymentTestFactory.H("other-policy"),
            plan.AnalysisBundleHash,
            plan.TopologyProjectionHash,
            plan.DevicePlans,
            plan.ActivationOrder,
            plan.RollbackOrder,
            plan.CreatedBy,
            plan.CreatedAtUtc,
            plan.ExpiresAtUtc);
        Assert.NotEqual(plan.PlanHash, shifted);
    }

    [Fact]
    public void GateRejectsDisabledNodeAndSecondNonterminal()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeploymentOperation first = DeploymentOperation.Create(plan, UserId.New(), T0);
        DeploymentOperationGate.EnsureCanStart(node, plan, [], T0);
        Assert.Throws<DomainInvariantException>(() =>
            DeploymentOperationGate.EnsureCanStart(node, plan, [first], T0));
        node.Disable();
        Assert.Throws<DomainInvariantException>(() =>
            DeploymentPlan.Create(
                node,
                DeploymentTestFactory.H("p"),
                DeploymentTestFactory.H("a"),
                DeploymentTestFactory.H("t"),
                plan.DevicePlans,
                UserId.New(),
                T0));
    }

    private static void AdvanceHappy(DeploymentOperation operation)
    {
        DeploymentOperationState[] path =
        [
            DeploymentOperationState.Prechecking,
            DeploymentOperationState.Staging,
            DeploymentOperationState.Staged,
            DeploymentOperationState.ArmingWatchdog,
            DeploymentOperationState.WatchdogArmed,
            DeploymentOperationState.Activating,
            DeploymentOperationState.Verifying,
            DeploymentOperationState.DisarmingWatchdog,
        ];
        DateTimeOffset t = T0;
        foreach (DeploymentOperationState next in path)
        {
            t = t.AddSeconds(1);
            operation.EnsureTransition(next, t);
        }
    }

    private static void AdvanceDeviceHappy(DeviceDeployment device)
    {
        DeviceDeploymentState[] path =
        [
            DeviceDeploymentState.Prechecked,
            DeviceDeploymentState.Staging,
            DeviceDeploymentState.Staged,
            DeviceDeploymentState.WatchdogArmed,
            DeviceDeploymentState.Activating,
            DeviceDeploymentState.ActiveUnverified,
            DeviceDeploymentState.Verified,
            DeviceDeploymentState.WatchdogDisarmed,
            DeviceDeploymentState.Committed,
        ];
        DateTimeOffset t = T0;
        foreach (DeviceDeploymentState next in path)
        {
            t = t.AddSeconds(1);
            device.EnsureTransition(next, t);
        }
    }
}
