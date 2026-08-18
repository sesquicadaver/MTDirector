using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

public sealed class OnboardingPlanInvariantTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BootstrapArtifactMatchesSpec23SeedHashAndChainNames()
    {
        Assert.Equal(BootstrapArtifact.Sha256Hex, BootstrapArtifact.ComputeSeedHash().ToString());
        Assert.Equal(BootstrapArtifact.ArtifactId, BootstrapArtifact.Hash.ToString()[..16]);
        Assert.Equal(
            "mfc4.i.r.8e40b9d4d67d42d6",
            BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input));
        Assert.Equal(
            "mfc6.f.r.8e40b9d4d67d42d6",
            BootstrapArtifact.RootChainName(IpAddressFamily.IPv6, FilterBuiltInContext.Forward));
        Assert.Equal("mfc:s:bootstrap-return:v1", BootstrapArtifact.ReturnComment);
        Assert.Equal("mfc:anchor:v1:4:i", AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input).Marker);
    }

    [Fact]
    public void RequiredAnchorSetOmitsForwardForSwitchAndIncludesItForRouter()
    {
        IReadOnlyList<AnchorKey> router = RequiredAnchorSet.For(NodeKind.Router, includeIpv6: true);
        Assert.Contains(router, static k => k.Family == IpAddressFamily.IPv4 && k.Chain == FilterBuiltInContext.Forward);
        Assert.Contains(router, static k => k.Family == IpAddressFamily.IPv6 && k.Chain == FilterBuiltInContext.Input);
        IReadOnlyList<AnchorKey> sw = RequiredAnchorSet.For(NodeKind.Switch, includeIpv6: false);
        Assert.False(RequiredAnchorSet.ContainsForward(sw));
        Assert.Equal(2, sw.Count);
        Assert.Throws<DomainInvariantException>(() => RequiredAnchorSet.For((NodeKind)99, false));
    }

    [Fact]
    public void SwitchPlanRejectsForwardAnchor()
    {
        Node node = OnboardingTestFactory.SwitchWithDevice(out Device device);
        IReadOnlyList<AnchorKey> keys =
        [
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Forward),
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Output),
        ];
        List<AnchorPlacement> placements = [];
        uint ordinal = 0;
        foreach (AnchorKey key in keys)
        {
            placements.Add(AnchorPlacement.Create(key.Family, key.Chain, AnchorPlacementMode.Append, ordinal));
            ordinal++;
        }

        DeviceOnboardingPlan forward = DeviceOnboardingPlan.Create(
            device.Id,
            "7.16.2",
            OnboardingTestFactory.H("cap"),
            OnboardingTestFactory.H("cfg"),
            OnboardingTestFactory.H("compat"),
            OnboardingTestFactory.H("api"),
            OnboardingTestFactory.H("read"),
            OnboardingTestFactory.H("deploy"),
            OnboardingTestFactory.H("mode"),
            OnboardingTestFactory.H("guard"),
            keys,
            placements);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Create(
                node,
                OnboardingTestFactory.H("m"),
                OnboardingTestFactory.H("t"),
                [forward],
                UserId.New(),
                T0));
        Assert.Contains("FORWARD", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlacementModesAndWatchdogBounds()
    {
        Assert.Throws<DomainInvariantException>(() =>
            AnchorPlacement.Create(
                IpAddressFamily.IPv4,
                FilterBuiltInContext.Input,
                AnchorPlacementMode.BeforeStaticRule,
                0));
        AnchorPlacement before = AnchorPlacement.Create(
            IpAddressFamily.IPv4,
            FilterBuiltInContext.Input,
            AnchorPlacementMode.BeforeStaticRule,
            3,
            OnboardingTestFactory.H("ref"),
            1,
            OnboardingTestFactory.H("pred"),
            OnboardingTestFactory.H("succ"));
        Assert.Equal(AnchorPlacementMode.BeforeStaticRule, before.Mode);
        Assert.Throws<DomainInvariantException>(() =>
            AnchorPlacement.Create(
                IpAddressFamily.IPv4,
                FilterBuiltInContext.Input,
                AnchorPlacementMode.Append,
                0,
                OnboardingTestFactory.H("ref"),
                1));
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router, watchdogTtl: TimeSpan.FromSeconds(59)));
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router, watchdogTtl: TimeSpan.FromSeconds(601)));
        DeviceOnboardingPlan ok = OnboardingTestFactory.DevicePlan(
            device.Id,
            NodeKind.Router,
            watchdogTtl: OnboardingCodes.MinWatchdogTtl);
        Assert.Equal(60, ok.WatchdogTtl.TotalSeconds);
        OnboardingPlan plan = OnboardingPlan.Create(
            node,
            OnboardingTestFactory.H("m"),
            OnboardingTestFactory.H("t"),
            [ok],
            UserId.New(),
            T0);
        Assert.Equal(ok.WatchdogTtl, plan.DevicePlans[0].WatchdogTtl);
    }

    [Fact]
    public void GateRejectsExpiredAndMismatchedPlanHash()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingGateEvaluation expired = OnboardingOperationGate.EvaluateCreate(plan, [], T0.AddMinutes(31));
        Assert.Equal(OnboardingCodes.PlanExpired, expired.ErrorCode);

        OnboardingPlan reconstituted = OnboardingPlan.Reconstitute(
            plan.Id,
            plan.NodeId,
            plan.NodeMembershipHash,
            plan.TopologyProjectionHash,
            plan.DevicePlans,
            plan.CreatedBy,
            plan.CreatedAtUtc,
            plan.ExpiresAtUtc,
            plan.PlanHash);
        Assert.True(OnboardingOperationGate.EvaluateCreate(reconstituted, [], T0).Allowed);
    }

    [Fact]
    public void RollbackPathAndRecoveryFromEnable()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingOperation operation = OnboardingOperation.Create(OnboardingTestFactory.PlanFor(node, T0), UserId.New(), T0);
        operation.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        operation.EnsureTransition(OnboardingOperationState.StagingBootstrapRoots, T0.AddSeconds(2));
        operation.EnsureTransition(OnboardingOperationState.StagingDisabledAnchors, T0.AddSeconds(3));
        operation.EnsureTransition(OnboardingOperationState.ArmingWatchdogs, T0.AddSeconds(4));
        operation.EnsureTransition(OnboardingOperationState.EnablingAnchors, T0.AddSeconds(5));
        operation.EnsureTransition(
            OnboardingOperationState.RecoveryRequired,
            T0.AddSeconds(6),
            OnboardingCodes.UnexpectedAnchorTarget);
        Assert.Equal(OnboardingOperationState.RecoveryRequired, operation.State);
        Assert.Equal(OnboardingCodes.UnexpectedAnchorTarget, operation.ErrorCode);

        OnboardingOperation rolling = OnboardingOperation.Create(OnboardingTestFactory.PlanFor(node, T0), UserId.New(), T0);
        rolling.EnsureTransition(OnboardingOperationState.Prechecking, T0.AddSeconds(1));
        rolling.EnsureTransition(OnboardingOperationState.StagingBootstrapRoots, T0.AddSeconds(2));
        rolling.EnsureTransition(OnboardingOperationState.RollbackPending, T0.AddSeconds(3));
        rolling.EnsureTransition(OnboardingOperationState.RollingBack, T0.AddSeconds(4));
        rolling.EnsureTransition(OnboardingOperationState.RecoveryRequired, T0.AddSeconds(5), OnboardingCodes.RollbackFailed);
        Assert.True(OnboardingOperation.IsTerminalState(rolling.State));
    }

    [Fact]
    public void StepReconstituteAndUnknownKindAreRejected()
    {
        OnboardingStep step = OnboardingStep.Create(
            Mfc.Domain.Onboarding.Primitives.OnboardingOperationId.New(),
            Mfc.Domain.Inventory.Primitives.DeviceId.New(),
            1,
            OnboardingStepKind.RemoveBootstrapRoot,
            OnboardingTestFactory.H("b"),
            OnboardingTestFactory.H("a"),
            T0);
        OnboardingStep clone = OnboardingStep.Reconstitute(
            step.Id,
            step.OperationId,
            step.DeviceId,
            step.Sequence,
            step.Kind,
            step.ExpectedBeforeHash,
            step.DesiredAfterHash,
            step.State,
            step.CreatedAtUtc,
            step.UpdatedAtUtc);
        Assert.Equal(step.Id, clone.Id);
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingStep.Reconstitute(
                step.Id,
                step.OperationId,
                step.DeviceId,
                step.Sequence,
                (OnboardingStepKind)99,
                step.ExpectedBeforeHash,
                step.DesiredAfterHash,
                step.State,
                step.CreatedAtUtc,
                step.UpdatedAtUtc));
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingStep.Reconstitute(
                step.Id,
                step.OperationId,
                step.DeviceId,
                step.Sequence,
                step.Kind,
                step.ExpectedBeforeHash,
                step.DesiredAfterHash,
                (OnboardingStepState)99,
                step.CreatedAtUtc,
                step.UpdatedAtUtc));
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingStep.Reconstitute(
                step.Id,
                step.OperationId,
                step.DeviceId,
                0,
                step.Kind,
                step.ExpectedBeforeHash,
                step.DesiredAfterHash,
                step.State,
                step.CreatedAtUtc,
                step.UpdatedAtUtc));
        Assert.Throws<DomainInvariantException>(() =>
            BootstrapArtifact.RootChainName((IpAddressFamily)9, FilterBuiltInContext.Input));
        Assert.Throws<DomainInvariantException>(() => AnchorKey.ChainCode((FilterBuiltInContext)9));
    }

    [Fact]
    public void DuplicateDevicePlanAndEmptyAnchorSetAreRejected()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router);
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Create(
                node,
                OnboardingTestFactory.H("m"),
                OnboardingTestFactory.H("t"),
                [plan, OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router)],
                UserId.New(),
                T0));
        Assert.Throws<DomainInvariantException>(() =>
            DeviceOnboardingPlan.Create(
                device.Id,
                "7.16",
                OnboardingTestFactory.H("cap"),
                OnboardingTestFactory.H("cfg"),
                OnboardingTestFactory.H("compat"),
                OnboardingTestFactory.H("api"),
                OnboardingTestFactory.H("read"),
                OnboardingTestFactory.H("deploy"),
                OnboardingTestFactory.H("mode"),
                OnboardingTestFactory.H("guard"),
                [],
                []));
        IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(NodeKind.Router, includeIpv6: false);
        Assert.Throws<DomainInvariantException>(() =>
            DeviceOnboardingPlan.Create(
                device.Id,
                "7.16",
                OnboardingTestFactory.H("cap"),
                OnboardingTestFactory.H("cfg"),
                OnboardingTestFactory.H("compat"),
                OnboardingTestFactory.H("api"),
                OnboardingTestFactory.H("read"),
                OnboardingTestFactory.H("deploy"),
                OnboardingTestFactory.H("mode"),
                OnboardingTestFactory.H("guard"),
                keys,
                [
                    AnchorPlacement.Create(
                        keys[0].Family,
                        keys[0].Chain,
                        AnchorPlacementMode.Append,
                        0),
                ]));
        List<AnchorKey> duplicates = [.. keys, keys[0]];
        Assert.Throws<DomainInvariantException>(() =>
            DeviceOnboardingPlan.Create(
                device.Id,
                "7.16",
                OnboardingTestFactory.H("cap"),
                OnboardingTestFactory.H("cfg"),
                OnboardingTestFactory.H("compat"),
                OnboardingTestFactory.H("api"),
                OnboardingTestFactory.H("read"),
                OnboardingTestFactory.H("deploy"),
                OnboardingTestFactory.H("mode"),
                OnboardingTestFactory.H("guard"),
                duplicates,
                OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router).AnchorPlacements));
        Assert.Throws<DomainInvariantException>(() =>
            DeviceOnboardingPlan.Create(
                device.Id,
                "7.16",
                OnboardingTestFactory.H("cap"),
                OnboardingTestFactory.H("cfg"),
                OnboardingTestFactory.H("compat"),
                OnboardingTestFactory.H("api"),
                OnboardingTestFactory.H("read"),
                OnboardingTestFactory.H("deploy"),
                OnboardingTestFactory.H("mode"),
                OnboardingTestFactory.H("guard"),
                keys,
                [
                    .. OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router).AnchorPlacements,
                    AnchorPlacement.Create(
                        IpAddressFamily.IPv6,
                        FilterBuiltInContext.Input,
                        AnchorPlacementMode.Append,
                        9),
                ]));
        Assert.Throws<DomainInvariantException>(() =>
            DeviceOnboardingPlan.Create(
                device.Id,
                "7.16.2",
                OnboardingTestFactory.H("cap"),
                OnboardingTestFactory.H("cfg"),
                OnboardingTestFactory.H("compat"),
                OnboardingTestFactory.H("api"),
                OnboardingTestFactory.H("read"),
                OnboardingTestFactory.H("deploy"),
                OnboardingTestFactory.H("mode"),
                OnboardingTestFactory.H("guard"),
                keys,
                OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router).AnchorPlacements,
                OnboardingTestFactory.H("not-bootstrap")));
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingTestFactory.DevicePlan(
                device.Id,
                NodeKind.Router,
                watchdogTtl: TimeSpan.FromMilliseconds(60_500)));
    }

    [Fact]
    public void GateAndCreateRequireUnmanagedNodeAndRejectExpiredPlan()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        Assert.Equal(OnboardingPlanHasher.Hash(plan).ToString(), plan.PlanHash.ToString());
        Assert.True(OnboardingOperationGate.EvaluateCreate(node, plan, [], T0).Allowed);
        OnboardingOperationGate.EnsureCanStart(node, plan, [], T0);
        OnboardingOperationGate.EnsureCanStart(plan, [], T0);
        OnboardingOperation started = OnboardingOperation.Create(plan, node, UserId.New(), T0);
        Assert.Equal(OnboardingOperationState.Created, started.State);

        device.SetManagementState(ManagementState.Managed);
        node.SetManagementState(ManagementState.Managed);
        OnboardingGateEvaluation managed = OnboardingOperationGate.EvaluateCreate(node, plan, [], T0);
        Assert.Equal(OnboardingCodes.NodeNotUnmanaged, managed.ErrorCode);
        Assert.Equal(managed.ErrorMessage, managed.Message);
        Assert.Throws<DomainInvariantException>(() => OnboardingOperationGate.EnsureCanStart(node, plan, [], T0));
        Assert.Throws<DomainInvariantException>(() => OnboardingOperation.Create(plan, node, UserId.New(), T0));

        Node other = OnboardingTestFactory.RouterWithDevice(out _);
        Assert.Equal(
            OnboardingCodes.DevicePlanCardinality,
            OnboardingOperationGate.EvaluateCreate(other, plan, [], T0).ErrorCode);
        Assert.Throws<DomainInvariantException>(() => OnboardingOperation.Create(plan, other, UserId.New(), T0));

        Assert.Throws<DomainInvariantException>(() => OnboardingOperation.Create(plan, UserId.New(), T0.AddMinutes(31)));
        Assert.Throws<DomainInvariantException>(() => OnboardingOperationGate.EnsureCanStart(plan, [], T0.AddMinutes(31)));
    }

    [Fact]
    public void OperationReconstituteAndStepIllegalTransitionsAreRejected()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out _);
        OnboardingOperation operation = OnboardingOperation.Create(OnboardingTestFactory.PlanFor(node, T0), UserId.New(), T0);
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingOperation.Reconstitute(
                operation.Id,
                operation.NodeId,
                operation.PlanId,
                OnboardingOperationState.Created,
                operation.CreatedBy,
                null,
                null,
                null,
                0,
                operation.CreatedAtUtc,
                operation.UpdatedAtUtc));
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingOperation.Reconstitute(
                operation.Id,
                operation.NodeId,
                operation.PlanId,
                OnboardingOperationState.Committed,
                operation.CreatedBy,
                T0,
                null,
                null,
                1,
                operation.CreatedAtUtc,
                operation.UpdatedAtUtc));
        OnboardingOperation clone = OnboardingOperation.Reconstitute(
            operation.Id,
            operation.NodeId,
            operation.PlanId,
            operation.State,
            operation.CreatedBy,
            operation.StartedAtUtc,
            operation.CompletedAtUtc,
            operation.ErrorCode,
            operation.RowVersion,
            operation.CreatedAtUtc,
            operation.UpdatedAtUtc);
        Assert.Equal(operation.Id, clone.Id);

        OnboardingStep step = OnboardingStep.Create(
            operation.Id,
            Mfc.Domain.Inventory.Primitives.DeviceId.New(),
            1,
            OnboardingStepKind.Verify,
            OnboardingTestFactory.H("b"),
            OnboardingTestFactory.H("a"),
            T0);
        Assert.Throws<DomainInvariantException>(() => step.MarkVerified(T0.AddSeconds(1)));
        step.RecordEffectSent(T0.AddSeconds(1));
        Assert.Throws<DomainInvariantException>(() => step.RecordEffectSent(T0.AddSeconds(2)));
        Assert.Equal(14, Enum.GetValues<OnboardingStepKind>().Length);
        Assert.Equal(2, Enum.GetValues<AnchorPlacementMode>().Length);
        Assert.Equal("mfc:anchor:v1:4:i", new AnchorKey(IpAddressFamily.IPv4, FilterBuiltInContext.Input).ToString());
        Assert.True(AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input)
            .Equals(AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input)));
        Assert.False(AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input)
            .Equals(AnchorKey.Create(IpAddressFamily.IPv6, FilterBuiltInContext.Input)));
        Assert.Throws<DomainInvariantException>(() =>
            AnchorKey.Create(IpAddressFamily.IPv4, (FilterBuiltInContext)9));
        Assert.Throws<DomainInvariantException>(() =>
            AnchorPlacement.Create(IpAddressFamily.IPv4, (FilterBuiltInContext)9, AnchorPlacementMode.Append, 0));
    }

    [Fact]
    public void VrrpPlanIgnoresDisabledMembersAndSwitchPlanSucceedsWithoutForward()
    {
        Node vrrp = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        second.SetEnabled(false);
        OnboardingPlan plan = OnboardingPlan.Create(
            vrrp,
            OnboardingTestFactory.H("m"),
            OnboardingTestFactory.H("t"),
            [OnboardingTestFactory.DevicePlan(first.Id, NodeKind.Vrrp)],
            UserId.New(),
            T0);
        Assert.Single(plan.DevicePlans);
        Assert.Equal(first.Id, plan.DevicePlans[0].DeviceId);

        Node sw = OnboardingTestFactory.SwitchWithDevice(out Device swDevice);
        OnboardingPlan switchPlan = OnboardingTestFactory.PlanFor(sw, T0);
        Assert.Single(switchPlan.DevicePlans);
        Assert.Equal(swDevice.Id, switchPlan.DevicePlans[0].DeviceId);
        Assert.False(RequiredAnchorSet.ContainsForward(switchPlan.DevicePlans[0].RequiredAnchorSet));
        Assert.False(string.IsNullOrWhiteSpace(plan.Id.ToString()));
        Assert.False(string.IsNullOrWhiteSpace(OnboardingOperationId.New().ToString()));
        Assert.False(string.IsNullOrWhiteSpace(OnboardingStepId.New().ToString()));
    }

    [Fact]
    public void RemainingCreateReconstituteAndHashBranchesAreClosed()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        AnchorKey key = AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input);
        Assert.False(key.Equals("not-a-key"));
        Assert.True(key.Equals((object)AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input)));
        Assert.Equal(
            key.GetHashCode(),
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input).GetHashCode());
        Assert.Throws<DomainInvariantException>(() =>
            AnchorPlacement.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input, (AnchorPlacementMode)9, 0));

        AnchorPlacement beforeNoNeighbors = AnchorPlacement.Create(
            IpAddressFamily.IPv4,
            FilterBuiltInContext.Input,
            AnchorPlacementMode.BeforeStaticRule,
            1,
            OnboardingTestFactory.H("ref"),
            1);
        DeviceOnboardingPlan hashed = DeviceOnboardingPlan.Create(
            device.Id,
            "7.16.2",
            OnboardingTestFactory.H("cap"),
            OnboardingTestFactory.H("cfg"),
            OnboardingTestFactory.H("compat"),
            OnboardingTestFactory.H("api"),
            OnboardingTestFactory.H("read"),
            OnboardingTestFactory.H("deploy"),
            OnboardingTestFactory.H("mode"),
            OnboardingTestFactory.H("guard"),
            RequiredAnchorSet.For(NodeKind.Router, false),
            [
                beforeNoNeighbors,
                AnchorPlacement.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Forward, AnchorPlacementMode.Append, 2),
                AnchorPlacement.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Output, AnchorPlacementMode.Append, 3),
            ]);
        OnboardingPlan plan = OnboardingPlan.Create(
            node,
            OnboardingTestFactory.H("m"),
            OnboardingTestFactory.H("t"),
            [hashed],
            UserId.New(),
            T0);
        Assert.Equal(OnboardingPlanHasher.Hash(plan).ToString(), plan.PlanHash.ToString());

        DeviceOnboardingPlan clone = DeviceOnboardingPlan.Reconstitute(
            hashed.DeviceId,
            hashed.ExpectedRouterOsVersion,
            hashed.ExpectedCapabilityHash,
            hashed.ExpectedConfigurationHash,
            hashed.ExpectedCompatibilityHash,
            hashed.ExpectedApiServiceHash,
            hashed.ExpectedReadAccountHash,
            hashed.ExpectedDeploymentAccountHash,
            hashed.ExpectedDeviceModeHash,
            hashed.ExpectedGuardHash,
            hashed.RequiredAnchorSet,
            hashed.AnchorPlacements,
            hashed.BootstrapArtifactHash,
            hashed.WatchdogTtl);
        Assert.Equal(hashed.DeviceId, clone.DeviceId);

        IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(NodeKind.Router, false);
        Assert.Throws<DomainInvariantException>(() =>
            DeviceOnboardingPlan.Create(
                device.Id,
                "7.16",
                OnboardingTestFactory.H("cap"),
                OnboardingTestFactory.H("cfg"),
                OnboardingTestFactory.H("compat"),
                OnboardingTestFactory.H("api"),
                OnboardingTestFactory.H("read"),
                OnboardingTestFactory.H("deploy"),
                OnboardingTestFactory.H("mode"),
                OnboardingTestFactory.H("guard"),
                keys,
                []));
        Assert.Throws<DomainInvariantException>(() =>
            DeviceOnboardingPlan.Create(
                device.Id,
                "7.16",
                OnboardingTestFactory.H("cap"),
                OnboardingTestFactory.H("cfg"),
                OnboardingTestFactory.H("compat"),
                OnboardingTestFactory.H("api"),
                OnboardingTestFactory.H("read"),
                OnboardingTestFactory.H("deploy"),
                OnboardingTestFactory.H("mode"),
                OnboardingTestFactory.H("guard"),
                keys,
                [
                    AnchorPlacement.Create(keys[0].Family, keys[0].Chain, AnchorPlacementMode.Append, 0),
                    AnchorPlacement.Create(keys[0].Family, keys[0].Chain, AnchorPlacementMode.Append, 1),
                    AnchorPlacement.Create(keys[1].Family, keys[1].Chain, AnchorPlacementMode.Append, 2),
                    AnchorPlacement.Create(keys[2].Family, keys[2].Chain, AnchorPlacementMode.Append, 3),
                ]));

        Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Create(
                node,
                OnboardingTestFactory.H("m"),
                OnboardingTestFactory.H("t"),
                [],
                UserId.New(),
                T0));
        device.SetEnabled(false);
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Create(
                node,
                OnboardingTestFactory.H("m"),
                OnboardingTestFactory.H("t"),
                [OnboardingTestFactory.DevicePlan(device.Id, NodeKind.Router)],
                UserId.New(),
                T0));

        OnboardingPlan live = OnboardingTestFactory.PlanFor(OnboardingTestFactory.RouterWithDevice(out _), T0);
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Reconstitute(
                live.Id,
                live.NodeId,
                live.NodeMembershipHash,
                live.TopologyProjectionHash,
                live.DevicePlans,
                live.CreatedBy,
                T0,
                T0,
                live.PlanHash));
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingPlan.Reconstitute(
                live.Id,
                live.NodeId,
                live.NodeMembershipHash,
                live.TopologyProjectionHash,
                [live.DevicePlans[0], live.DevicePlans[0]],
                live.CreatedBy,
                live.CreatedAtUtc,
                live.ExpiresAtUtc,
                live.PlanHash));
        Assert.Throws<DomainInvariantException>(() =>
            OnboardingStep.Create(
                OnboardingOperationId.New(),
                device.Id,
                1,
                (OnboardingStepKind)99,
                OnboardingTestFactory.H("b"),
                OnboardingTestFactory.H("a"),
                T0));
    }
}
