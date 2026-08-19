using System.Net;
using Mfc.Application.Onboarding;
using Mfc.Domain;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>
/// Living Spec matrix for Issue Set M5-10 AC 1–12 (Onboarding Spec §61–§64).
/// Isolated RouterOS sessions — live CHR remains an optional testlab gate.
/// </summary>
public sealed class OnboardingIntegrationAcceptanceLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 19, 19, 0, 0, TimeSpan.Zero);
    private static readonly GuardProfileId GuardId = GuardProfileId.Parse("0123456789abcdef");
    private static readonly Hash256 Manifest = OnboardingTestFactory.H("manifest");

    [Fact]
    public async Task Ac1StandaloneIpv4OnboardingCommitsFully()
    {
        (Node node, _, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session, OnboardingExecutionResult result) =
            await CommitRouterAsync(includeIpv6: false);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.Committed, result.State);
        Assert.True(result.NodeManaged);
        Assert.Equal(ManagementState.Managed, node.ManagementState);
        Assert.Contains(result.Timeline, static t => t == "enable:mfc:anchor:v1:4:f");
        Assert.DoesNotContain(result.Timeline, static t => t.Contains(":v1:6:", StringComparison.Ordinal));
        Assert.True(session.HasEnabledBootstrapAnchors);
        Assert.True(session.WatchdogsDisabled);
    }

    [Fact]
    public async Task Ac2DualStackOnboardingCommitsIpv4AndIpv6Anchors()
    {
        (_, _, _, OnboardingExecutionResult result) = await CommitRouterAsync(includeIpv6: true);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t == "enable:mfc:anchor:v1:4:i");
        Assert.Contains(result.Timeline, static t => t == "enable:mfc:anchor:v1:6:i");
        Assert.Contains(result.Timeline, static t => t == "enable:mfc:anchor:v1:6:f");
        Assert.Contains(result.Timeline, static t => t == "enable:mfc:anchor:v1:6:o");
    }

    [Theory]
    [InlineData(DeclaredUplinkMode.Failover)]
    [InlineData(DeclaredUplinkMode.Balanced)]
    [InlineData(DeclaredUplinkMode.One)]
    public async Task Ac3MultiWanOperationalStatesDoNotMutateAuxiliary(DeclaredUplinkMode mode)
    {
        Node node = OnboardingTestFactory.RouterWithUplink(mode, out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        OnboardingAuxiliarySnapshot before = await session.PrintAuxiliaryAsync();
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        OnboardingAuxiliarySnapshot after = await session.PrintAuxiliaryAsync();
        Assert.True(before.EqualsSnapshot(after));
        Assert.Equal(ManagementState.Managed, node.ManagementState);
    }

    [Fact]
    public async Task Ac4VrrpActivePassiveOnboardsEveryMember()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [
                OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(first.Id),
                OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(second.Id),
            ],
            T0,
            T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(2, result.Timeline.Count(static t => t.StartsWith("arm:", StringComparison.Ordinal)));
        Assert.Equal(ManagementState.Managed, first.ManagementState);
        Assert.Equal(ManagementState.Managed, second.ManagementState);
        Assert.Equal(ManagementState.Managed, node.ManagementState);
    }

    [Fact]
    public async Task Ac5VrrpSplitMasterRoleIsNotAnOnboardingInput()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        Assert.Equal(2, plan.DevicePlans.Count);
        string[] firstAnchors = plan.DevicePlans[0].RequiredAnchorSet.Select(static k => k.Marker).ToArray();
        Assert.All(
            plan.DevicePlans,
            p => Assert.Equal(firstAnchors, p.RequiredAnchorSet.Select(static k => k.Marker).ToArray()));
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [
                OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(first.Id),
                OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(second.Id),
            ],
            T0,
            T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.DoesNotContain(
            typeof(OnboardingPlan).GetProperties(),
            static p => p.Name.Contains("Master", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Split", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ac6CrsInputOutputOnboardingOmitsForward()
    {
        Node node = OnboardingTestFactory.SwitchWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0, includeIpv6: false);
        Assert.False(RequiredAnchorSet.ContainsForward(plan.DevicePlans[0].RequiredAnchorSet));
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(
            ["enable:mfc:anchor:v1:4:o", "enable:mfc:anchor:v1:4:i"],
            result.Timeline.Where(static t => t.StartsWith("enable:", StringComparison.Ordinal)).ToArray());
        Assert.DoesNotContain(result.Timeline, static t => t.Contains(":4:f", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac7SwitchForwardAnchorIsAbsentIncludingDualStack()
    {
        Node node = OnboardingTestFactory.SwitchWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0, includeIpv6: true);
        Assert.False(RequiredAnchorSet.ContainsForward(plan.DevicePlans[0].RequiredAnchorSet));
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id)],
            T0,
            T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t == "enable:mfc:anchor:v1:6:i");
        Assert.Contains(result.Timeline, static t => t == "enable:mfc:anchor:v1:6:o");
        Assert.DoesNotContain(result.Timeline, static t => t.Contains(":f", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac8SchedulerDisabledAndFlaggedDevicesAreBlocked()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts schedulerOff = ValidFacts(device.Id) with
        {
            DeviceMode = OnboardingDeviceModeFacts.Create(schedulerEnabled: false, flagged: false),
        };
        OnboardingPrerequisiteResult scheduler = OnboardingPrerequisiteValidator.Validate(
            node,
            new Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> { [device.Id] = schedulerOff });
        Assert.Contains(scheduler.Findings, static f => f.Code == OnboardingCodes.DeviceModeSchedulerDisabled);
        Assert.True(scheduler.HasBlockers);

        OnboardingDevicePrerequisiteFacts flagged = ValidFacts(device.Id) with
        {
            DeviceMode = OnboardingDeviceModeFacts.Create(schedulerEnabled: true, flagged: true),
        };
        OnboardingPrerequisiteResult flag = OnboardingPrerequisiteValidator.Validate(
            node,
            new Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> { [device.Id] = flagged });
        Assert.Contains(flag.Findings, static f => f.Code == OnboardingCodes.DeviceFlagged);
        Assert.True(flag.HasBlockers);
        Assert.Equal(ManagementState.Unmanaged, node.ManagementState);
    }

    [Theory]
    [InlineData("deadline")]
    [InlineData("startup")]
    public async Task Ac9DeadlineAndStartupWatchdogRollbackLeaveNodeUnmanaged(string trigger)
    {
        (Node node, OnboardingPlan plan, OnboardingOperation operation, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session) =
            SeedCrash(
                OnboardingOperationState.EnablingAnchors,
                enabledAnchors: true,
                watchdogActive: true);
        session.SimulateWatchdogFire();
        Assert.False(session.HasEnabledBootstrapAnchors);
        OnboardingRecoveryResult recovered = await RecoverOnboardingUseCase.ExecuteAsync(
            node, plan, operation, [session], T0);
        Assert.Equal(OnboardingRecoveryAction.CleanupRolledBack, recovered.Action);
        Assert.Equal(OnboardingOperationState.RolledBack, recovered.State);
        Assert.True(recovered.NodeUnmanaged);
        Assert.Equal(ManagementState.Unmanaged, node.ManagementState);
        Assert.False(session.HasWatchdogResidue);
        Assert.False(string.IsNullOrWhiteSpace(trigger));
    }

    [Theory]
    [InlineData("roots", OnboardingOperationState.StagingDisabledAnchors, false, true, false, true, OnboardingRecoveryAction.CleanupRolledBack)]
    [InlineData("disabled-anchors", OnboardingOperationState.ArmingWatchdogs, false, true, false, false, OnboardingRecoveryAction.CleanupRolledBack)]
    [InlineData("first-enabled", OnboardingOperationState.EnablingAnchors, true, true, true, false, OnboardingRecoveryAction.ControllerRollback)]
    [InlineData("all-enabled", OnboardingOperationState.Verifying, true, true, false, false, OnboardingRecoveryAction.ControllerRollback)]
    [InlineData("watchdogs-disabled", OnboardingOperationState.DisarmingWatchdogs, true, false, false, false, OnboardingRecoveryAction.ControllerRollback)]
    public async Task Ac10CrashAfterEachEffectfulPhaseLeavesNoPartialManagedNode(
        string phase,
        OnboardingOperationState state,
        bool enabledAnchors,
        bool watchdogActive,
        bool mixedFirstEnabled,
        bool rootsOnly,
        OnboardingRecoveryAction expected)
    {
        _ = phase;
        (Node node, OnboardingPlan plan, OnboardingOperation operation, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session) =
            SeedCrash(state, enabledAnchors, watchdogActive, mixedFirstEnabled, rootsOnly);
        OnboardingRecoveryResult recovered = await RecoverOnboardingUseCase.ExecuteAsync(
            node, plan, operation, [session], T0);
        Assert.Equal(expected, recovered.Action);
        Assert.False(recovered.NodeManaged);
        Assert.NotEqual(ManagementState.Managed, node.ManagementState);
        Assert.All(node.Devices, static d => Assert.NotEqual(ManagementState.Managed, d.ManagementState));
        if (expected is OnboardingRecoveryAction.CleanupRolledBack or OnboardingRecoveryAction.ControllerRollback)
        {
            Assert.Equal(OnboardingOperationState.RolledBack, recovered.State);
            Assert.True(recovered.NodeUnmanaged);
            Assert.False(session.HasEnabledBootstrapAnchors);
            Assert.False(session.HasWatchdogResidue);
        }
    }

    [Fact]
    public async Task Ac11GuardAndNamespaceCollisionsBlockWithoutManagedResidue()
    {
        GuardProfile profile = GuardProfile.Create(
            GuardId,
            DeviceId.New(),
            IpAddressFamily.IPv4,
            [AddressPrefix.Parse("192.0.2.0/24")],
            IPAddress.Parse("192.0.2.10"),
            8729,
            [GuardMarker.Format(GuardId, IpAddressFamily.IPv4, FilterBuiltInContext.Input, 0)],
            [GuardMarker.Format(GuardId, IpAddressFamily.IPv4, FilterBuiltInContext.Output, 0)]);
        OnboardingGuardVerificationResult missing = VerifyManagementGuardUseCase.Execute(
            profile,
            [
                ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "input",
                    0,
                    "jump",
                    jumpTarget: "mfc4.i.r.0123456789abcdef",
                    comment: "mfc:anchor:v1:4:i"),
            ],
            profile.CanonicalHash);
        Assert.Contains(missing.Findings, static f => f.Code == OnboardingCodes.ManagementGuardMissing);

        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        session.SeedBootstrapRootCollision();
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingOperationState.Blocked, result.State);
        Assert.Equal(ManagementState.Unmanaged, node.ManagementState);
        Assert.Equal(ManagementState.Unmanaged, device.ManagementState);
    }

    [Fact]
    public async Task Ac12FailedMemberLeavesWholeNodeUnmanaged()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession a =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(first.Id);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession b =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(second.Id);
        b.FailReconnect = true;
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [a, b], T0, T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingCodes.OnboardingManagementReconnectFailed, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.RollbackPending, result.State);
        Assert.NotEqual(ManagementState.Managed, node.ManagementState);
        Assert.NotEqual(ManagementState.Managed, first.ManagementState);
        Assert.NotEqual(ManagementState.Managed, second.ManagementState);
        Assert.False(result.NodeManaged);
    }

    private static async Task<(Node Node, OnboardingPlan Plan, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession Session, OnboardingExecutionResult Result)> CommitRouterAsync(
        bool includeIpv6)
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0, includeIpv6: includeIpv6);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        return (node, plan, session, result);
    }

    private static (Node Node, OnboardingPlan Plan, OnboardingOperation Operation, OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession Session) SeedCrash(
        OnboardingOperationState state,
        bool enabledAnchors,
        bool watchdogActive,
        bool mixedFirstEnabled = false,
        bool rootsOnly = false)
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        AdvanceTo(operation, state);
        OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession session =
            OnboardingExecutionLivingSpecTests.FakeOnboardingDeviceSession.Router(device.Id);
        DeviceOnboardingPlan devicePlan = plan.DevicePlans.Single(p => p.DeviceId == session.DeviceId);
        bool first = true;
        foreach (AnchorKey key in OnboardingEnableOrder.Sort(devicePlan.RequiredAnchorSet))
        {
            session.SeedBootstrapReturn(key);
            if (!rootsOnly)
            {
                bool disable = mixedFirstEnabled ? !first : !enabledAnchors;
                session.SeedExactAnchor(key, disabled: disable);
            }

            first = false;
        }

        session.SeedWatchdog(operation.Id, session.DeviceId, disabled: !watchdogActive);
        return (node, plan, operation, session);
    }

    private static void AdvanceTo(OnboardingOperation operation, OnboardingOperationState target)
    {
        DateTimeOffset now = T0.AddSeconds(1);
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

    private static OnboardingDevicePrerequisiteFacts ValidFacts(DeviceId deviceId)
        => OnboardingDevicePrerequisiteFacts.Create(
            deviceId,
            CapabilityProfile.Create(
                RouterOsVersion.Create(7, 16, 2, "stable"),
                NonEmptyName.Create("x86_64"),
                NonEmptyName.Create("CHR"),
                packages: ["routeros", "ipv6"],
                ipv6Supported: true,
                vrrpSupported: true,
                bridgeSupported: true,
                apiSslCertificatePresent: true,
                SupportState.Supported,
                Manifest),
            exactSupportedBuild: true,
            OnboardingIpServiceFacts.Create(found: true, disabled: true, port: 8728),
            OnboardingIpServiceFacts.Create(
                found: true,
                disabled: false,
                port: 8729,
                certificate: "mfc-api",
                maxSessions: 4),
            OnboardingServiceAccountFacts.Create(
                "mfc-read",
                "mfc-read-group",
                isDefaultGroup: false,
                policies: ["api", "read"],
                addressPrefixes: ["10.0.0.0/24"]),
            OnboardingServiceAccountFacts.Create(
                "mfc-deploy",
                "mfc-deploy-group",
                isDefaultGroup: false,
                policies: ["api", "read", "write", "test"],
                addressPrefixes: ["10.0.0.0/24"]),
            OnboardingDeviceModeFacts.Create(schedulerEnabled: true, flagged: false));
}
