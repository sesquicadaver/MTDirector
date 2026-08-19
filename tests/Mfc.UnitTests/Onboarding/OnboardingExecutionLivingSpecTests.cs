using System.Globalization;
using Mfc.Application.Onboarding;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Onboarding;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>
/// Living Spec matrix for Issue Set M5-07 AC 1–13 (Onboarding Spec §29 / §37–§43).
/// </summary>
public sealed class OnboardingExecutionLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 19, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ac1RootsAreStagedBeforeAnchors()
    {
        OnboardingExecutionResult result = await RunRouterAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        List<string> timeline = [.. result.Timeline];
        int firstAnchor = timeline.FindIndex(static t => t.StartsWith("anchor-disabled:", StringComparison.Ordinal));
        int lastRoot = timeline.FindLastIndex(static t => t.StartsWith("root:", StringComparison.Ordinal));
        Assert.True(lastRoot >= 0 && firstAnchor > lastRoot);
    }

    [Fact]
    public async Task Ac2AllAnchorsAreStagedDisabled()
    {
        (_, FakeOnboardingDeviceSession session, OnboardingExecutionResult result) = await RunRouterDetailedAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t.StartsWith("anchor-disabled:mfc:anchor:v1:4:i", StringComparison.Ordinal));
        Assert.Contains(result.Timeline, static t => t.StartsWith("anchor-disabled:mfc:anchor:v1:4:f", StringComparison.Ordinal));
        Assert.Contains(result.Timeline, static t => t.StartsWith("anchor-disabled:mfc:anchor:v1:4:o", StringComparison.Ordinal));
        Assert.All(
            session.StagedDisabledMarkers,
            static marker => Assert.StartsWith("mfc:anchor:v1:", marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac3VrrpWatchdogsAreArmedBeforeFirstEnable()
    {
        OnboardingExecutionResult result = await RunVrrpAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        List<string> timeline = [.. result.Timeline];
        int firstEnable = timeline.FindIndex(static t => t.StartsWith("enable:", StringComparison.Ordinal));
        int lastArm = timeline.FindLastIndex(static t => t.StartsWith("arm:", StringComparison.Ordinal));
        Assert.Equal(2, result.Timeline.Count(static t => t.StartsWith("arm:", StringComparison.Ordinal)));
        Assert.True(lastArm >= 0 && firstEnable > lastArm);
    }

    [Fact]
    public async Task Ac4AnchorEnableOrderIsNormative()
    {
        OnboardingExecutionResult result = await RunRouterAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        string[] enables = result.Timeline.Where(static t => t.StartsWith("enable:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(
            [
                "enable:mfc:anchor:v1:4:f",
                "enable:mfc:anchor:v1:4:o",
                "enable:mfc:anchor:v1:4:i",
            ],
            enables);
        AnchorKey[] keys =
        [
            AnchorKey.Create(IpAddressFamily.IPv6, FilterBuiltInContext.Forward),
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Forward),
        ];
        Assert.Equal(
            ["mfc:anchor:v1:4:f", "mfc:anchor:v1:6:f", "mfc:anchor:v1:4:i"],
            OnboardingEnableOrder.Sort(keys).Select(static k => k.Marker).ToArray());
    }

    [Fact]
    public async Task Ac5EachEnableHasReadBack()
    {
        OnboardingExecutionResult result = await RunRouterAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(3, result.Timeline.Count(static t => t.StartsWith("enable:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Ac6NewApiConnectionOpensAfterManagementAnchors()
    {
        OnboardingExecutionResult result = await RunRouterAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        List<string> timeline = [.. result.Timeline];
        int output = timeline.IndexOf("enable:mfc:anchor:v1:4:o");
        int input = timeline.IndexOf("enable:mfc:anchor:v1:4:i");
        int reconnect = timeline.FindIndex(static t => t.StartsWith("reconnect:", StringComparison.Ordinal));
        Assert.True(output >= 0 && input > output && reconnect > input);
    }

    [Fact]
    public async Task Ac7StablePostBootstrapCaptureRuns()
    {
        OnboardingExecutionResult result = await RunRouterAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.True(result.CapturePerformed);
        Assert.Contains(result.Timeline, static t => t.StartsWith("capture:", StringComparison.Ordinal));
        List<string> timeline = [.. result.Timeline];
        int lastEnable = timeline.FindLastIndex(static t => t.StartsWith("enable:", StringComparison.Ordinal));
        int capture = timeline.FindIndex(static t => t.StartsWith("capture:", StringComparison.Ordinal));
        int disarm = timeline.FindIndex(static t => t.StartsWith("disarm:", StringComparison.Ordinal));
        Assert.True(capture > lastEnable && disarm > capture);
    }

    [Fact]
    public async Task Ac8UnmanagedRulesAndRelativeOrderAreUnchanged()
    {
        (OnboardingPlan plan, FakeOnboardingDeviceSession session, OnboardingExecutionResult result) = await RunRouterDetailedAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        IReadOnlyList<ActualFilterRule> after = await session.PrintFilterAsync();
        OnboardingEquivalenceResult eq = OnboardingPassThroughEquivalence.Evaluate(session.InitialFilter, after);
        Assert.Equal(OnboardingEquivalenceVerdict.Proven, eq.Verdict);
        Assert.NotEmpty(plan.DevicePlans);
    }

    [Fact]
    public async Task Ac9NatRawMangleRoutingVrrpAreUnchanged()
    {
        OnboardingExecutionResult ok = await RunRouterAsync();
        Assert.True(ok.Succeeded, ok.ErrorCode);

        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        session.MutateAuxiliaryAfterCapture = true;
        OnboardingExecutionResult mutated = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0,
            T0);
        Assert.False(mutated.Succeeded);
        Assert.Equal(OnboardingCodes.OnboardingAuxiliaryMutated, mutated.ErrorCode);
        Assert.Equal(OnboardingOperationState.RollbackPending, mutated.State);
        Assert.Equal(ManagementState.Unmanaged, node.ManagementState);
    }

    [Fact]
    public async Task Ac10SemanticEquivalencePassThroughIsProven()
    {
        OnboardingExecutionResult result = await RunRouterAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.Committed, result.State);
    }

    [Fact]
    public async Task Ac11IndeterminateEquivalenceStartsRollback()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        session.InjectUnknownMatcherOnCapture = true;
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0,
            T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingCodes.BootstrapSemanticEquivalenceNotProven, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.RollbackPending, result.State);
        Assert.False(result.NodeManaged);
    }

    [Fact]
    public async Task Ac12WatchdogsAreDisabledBeforeDurableCommit()
    {
        (_, FakeOnboardingDeviceSession session, OnboardingExecutionResult result) = await RunRouterDetailedAsync();
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.True(result.WatchdogsDisarmed);
        List<string> timeline = [.. result.Timeline];
        int disarm = timeline.FindIndex(static t => t.StartsWith("disarm:", StringComparison.Ordinal));
        Assert.True(disarm >= 0);
        Assert.True(session.WatchdogsDisabled);
        Assert.Equal(OnboardingOperationState.Committed, result.State);
    }

    [Fact]
    public async Task Ac13NodeBecomesManagedOnlyFully()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession a = FakeOnboardingDeviceSession.Router(first.Id);
        FakeOnboardingDeviceSession b = FakeOnboardingDeviceSession.Router(second.Id);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [a, b],
            T0,
            T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(ManagementState.Managed, first.ManagementState);
        Assert.Equal(ManagementState.Managed, second.ManagementState);
        Assert.Equal(ManagementState.Managed, node.ManagementState);
        Assert.True(result.NodeManaged);
    }

    [Fact]
    public async Task NamespaceCollisionBlocksBeforeStaging()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        session.SeedBootstrapRootCollision();
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingOperationState.Blocked, result.State);
        Assert.False(string.IsNullOrEmpty(result.ErrorCode));
        Assert.Contains(result.Timeline, static t => t.StartsWith("blocked:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WatchdogNameCollisionRollsBackWhileArming()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        session.SeedWatchdogResidue();
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingOperationState.RollbackPending, result.State);
        Assert.True(
            result.ErrorCode == OnboardingCodes.OnboardingWatchdogCollision
            || result.ErrorCode == OnboardingCodes.MfcNamespaceCollision,
            result.ErrorCode);
    }

    [Fact]
    public async Task FailedArmRollsBack()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        session.Watchdog = new ScriptedWatchdog(session.Watchdog, failArm: true);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingCodes.OnboardingWatchdogArmFailed, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.RollbackPending, result.State);
    }

    [Fact]
    public async Task FailedEnableReadBackRollsBack()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        session.Bootstrap = new ScriptedBootstrap(session.Bootstrap, failEnable: true);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingCodes.RollbackFailed, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.RollbackPending, result.State);
    }

    [Fact]
    public async Task FailedReconnectRollsBack()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        session.FailReconnect = true;
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingCodes.OnboardingManagementReconnectFailed, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.RollbackPending, result.State);
    }

    [Fact]
    public async Task FailedDisarmRollsBackAfterCapture()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        session.Watchdog = new ScriptedWatchdog(session.Watchdog, failDisarm: true);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.False(result.Succeeded);
        Assert.True(result.CapturePerformed);
        Assert.Equal(OnboardingCodes.OnboardingWatchdogDisableFailed, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.RollbackPending, result.State);
    }

    [Fact]
    public async Task FailedStagingWriteRollsBackWithError()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        session.Bootstrap = new ScriptedBootstrap(session.Bootstrap, failAdd: true);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingCodes.RollbackFailed, result.ErrorCode);
        Assert.Contains(result.Timeline, static t => t.StartsWith("error:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectsNullArgumentsAndMissingSession()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ExecuteOnboardingBootstrapUseCase.ExecuteAsync(null!, plan, operation, [], T0, T0));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ExecuteOnboardingBootstrapUseCase.ExecuteAsync(node, null!, operation, [], T0, T0));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ExecuteOnboardingBootstrapUseCase.ExecuteAsync(node, plan, null!, [], T0, T0));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ExecuteOnboardingBootstrapUseCase.ExecuteAsync(node, plan, operation, null!, T0, T0));
        await Assert.ThrowsAsync<DomainInvariantException>(() =>
            ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
                node,
                plan,
                operation,
                [FakeOnboardingDeviceSession.Router(DeviceId.New())],
                T0,
                T0));
    }

    [Fact]
    public async Task SkipsAdvanceWhenAlreadyInRequestedState()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        operation.EnsureTransition(OnboardingOperationState.Prechecking, T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node, plan, operation, [session], T0, T0);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(OnboardingOperationState.Committed, result.State);
    }

    [Fact]
    public void AuxiliarySnapshotEqualityIsFieldWise()
    {
        OnboardingAuxiliarySnapshot a = new()
        {
            NatHash = OnboardingTestFactory.H("nat"),
            RawHash = OnboardingTestFactory.H("raw"),
            MangleHash = OnboardingTestFactory.H("mangle"),
            RoutingHash = OnboardingTestFactory.H("routing"),
            VrrpHash = OnboardingTestFactory.H("vrrp"),
            InterfaceListHash = OnboardingTestFactory.H("iflist"),
        };
        Assert.True(a.EqualsSnapshot(a));
        Assert.Throws<ArgumentNullException>(() => a.EqualsSnapshot(null!));
    }

    [Fact]
    public void PassThroughEquivalenceRejectsMutatedReturnAndUnmanagedJump()
    {
        ActualFilterRule accept = ActualFilterRule.Create(IpAddressFamily.IPv4, "input", 0, "accept", comment: "user-input");
        string root = BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input);
        ActualFilterRule badReturn = ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            root,
            0,
            "return",
            comment: BootstrapArtifact.ReturnComment,
            knownMatchers: new Dictionary<string, string>(StringComparer.Ordinal) { ["src-address"] = "1.1.1.1" });
        OnboardingEquivalenceResult notProven = OnboardingPassThroughEquivalence.Evaluate([accept], [accept, badReturn]);
        Assert.Equal(OnboardingEquivalenceVerdict.NotProven, notProven.Verdict);

        ActualFilterRule unmanagedJump = ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "input",
            1,
            "jump",
            jumpTarget: root,
            comment: "foreign");
        OnboardingEquivalenceResult indeterminate = OnboardingPassThroughEquivalence.Evaluate(
            [accept],
            [accept, unmanagedJump]);
        Assert.Equal(OnboardingEquivalenceVerdict.Indeterminate, indeterminate.Verdict);
    }

    private static Task<OnboardingExecutionResult> RunRouterAsync()
        => RunRouterDetailedAsync().ContinueWith(static t => t.Result.Result);

    private static async Task<(OnboardingPlan Plan, FakeOnboardingDeviceSession Session, OnboardingExecutionResult Result)> RunRouterDetailedAsync()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        FakeOnboardingDeviceSession session = FakeOnboardingDeviceSession.Router(device.Id);
        OnboardingExecutionResult result = await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [session],
            T0,
            T0);
        return (plan, session, result);
    }

    private static async Task<OnboardingExecutionResult> RunVrrpAsync()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        OnboardingPlan plan = OnboardingTestFactory.PlanFor(node, T0);
        OnboardingOperation operation = OnboardingOperation.Create(plan, UserId.New(), T0);
        return await ExecuteOnboardingBootstrapUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            [FakeOnboardingDeviceSession.Router(first.Id), FakeOnboardingDeviceSession.Router(second.Id)],
            T0,
            T0);
    }

    internal sealed class FakeOnboardingDeviceSession : IOnboardingDeviceSession
    {
        private readonly CombinedChannel _channel;
        private readonly OnboardingAuxiliarySnapshot _auxiliary;

        private FakeOnboardingDeviceSession(DeviceId deviceId, CombinedChannel channel)
        {
            DeviceId = deviceId;
            _channel = channel;
            Bootstrap = new OnboardingBootstrapWriter(channel);
            Watchdog = new OnboardingWatchdogWriter(channel);
            _auxiliary = NewAuxiliary();
        }

        public DeviceId DeviceId { get; }

        public IOnboardingBootstrapWritePort Bootstrap { get; set; }

        public IOnboardingWatchdogPort Watchdog { get; set; }

        public bool MutateAuxiliaryAfterCapture { get; set; }

        public bool InjectUnknownMatcherOnCapture { get; set; }

        public bool FailReconnect { get; set; }

        public bool WatchdogsDisabled => _channel.SchedulersDisabled;

        public bool HasWatchdogResidue => _channel.HasWatchdogResidue;

        public bool HasEnabledBootstrapAnchors => _channel.HasEnabledBootstrapAnchors;

        public bool HasBootstrapRoots => _channel.HasBootstrapRoots;

        public IReadOnlyList<string> UserComments => _channel.UserComments;

        public IReadOnlyList<string> StagedDisabledMarkers => _channel.StagedDisabledMarkers;

        public IReadOnlyList<ActualFilterRule> InitialFilter => _channel.InitialFilter;

        public static FakeOnboardingDeviceSession Router(DeviceId deviceId)
            => new(deviceId, CombinedChannel.WithUnmanagedBuiltins());

        public void SeedBootstrapRootCollision() => _channel.SeedBootstrapRootCollision();

        public void SeedWatchdogResidue() => _channel.SeedWatchdogResidue();

        public void SeedExactAnchor(AnchorKey key, bool disabled, string? jumpTarget = null)
            => _channel.SeedExactAnchor(key, disabled, jumpTarget);

        public void SeedBootstrapReturn(AnchorKey key) => _channel.SeedBootstrapReturn(key);

        public void SeedWatchdog(OnboardingOperationId operationId, DeviceId deviceId, bool disabled)
            => _channel.SeedWatchdog(operationId, deviceId, disabled);

        public Task<IReadOnlyList<ActualFilterRule>> PrintFilterAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ActualFilterRule>>(_channel.ToFilterRules());

        public Task<OnboardingSystemNameFacts> PrintSystemNamesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new OnboardingSystemNameFacts
            {
                ScriptNames = _channel.ScriptNames(),
                SchedulerNames = _channel.SchedulerNames(),
                SchedulerDisabled = _channel.SchedulerDisabledMap(),
            });

        public Task<OnboardingAuxiliarySnapshot> PrintAuxiliaryAsync(CancellationToken cancellationToken = default)
        {
            if (MutateAuxiliaryAfterCapture && _channel.Captured)
            {
                return Task.FromResult(new OnboardingAuxiliarySnapshot
                {
                    NatHash = OnboardingTestFactory.H("nat-changed"),
                    RawHash = _auxiliary.RawHash,
                    MangleHash = _auxiliary.MangleHash,
                    RoutingHash = _auxiliary.RoutingHash,
                    VrrpHash = _auxiliary.VrrpHash,
                    InterfaceListHash = _auxiliary.InterfaceListHash,
                });
            }

            return Task.FromResult(_auxiliary);
        }

        public Task<bool> ReconnectManagementAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(!FailReconnect);

        public Task<IReadOnlyList<ActualFilterRule>> CaptureStableAsync(CancellationToken cancellationToken = default)
        {
            _channel.Captured = true;
            IReadOnlyList<ActualFilterRule> rules = _channel.ToFilterRules();
            if (InjectUnknownMatcherOnCapture)
            {
                List<ActualFilterRule> mutated = [.. rules];
                mutated.Add(ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "input",
                    mutated.Count,
                    "accept",
                    comment: "opaque",
                    unknownMatchers: new Dictionary<string, string>(StringComparer.Ordinal) { ["mystery"] = "yes" }));
                return Task.FromResult<IReadOnlyList<ActualFilterRule>>(mutated);
            }

            return Task.FromResult(rules);
        }

        private static OnboardingAuxiliarySnapshot NewAuxiliary()
            => new()
            {
                NatHash = OnboardingTestFactory.H("nat"),
                RawHash = OnboardingTestFactory.H("raw"),
                MangleHash = OnboardingTestFactory.H("mangle"),
                RoutingHash = OnboardingTestFactory.H("routing"),
                VrrpHash = OnboardingTestFactory.H("vrrp"),
                InterfaceListHash = OnboardingTestFactory.H("iflist"),
            };
    }

    private sealed class CombinedChannel : IOnboardingWriteChannel
    {
        private readonly List<Dictionary<string, string>> _filters = [];
        private readonly List<Dictionary<string, string>> _scripts = [];
        private readonly List<Dictionary<string, string>> _schedulers = [];
        private readonly List<ActualFilterRule> _initial = [];
        private int _nextId = 1;

        public bool Captured { get; set; }

        public IReadOnlyList<ActualFilterRule> InitialFilter => _initial;

        public bool SchedulersDisabled
            => _schedulers.Count > 0 && _schedulers.All(static r => r.GetValueOrDefault("disabled") is "yes" or "true");

        public bool HasWatchdogResidue
            => _scripts.Any(static r => OnboardingWatchdogNames.IsOnboardingWatchdogName(r.GetValueOrDefault("name"))
                                        || OnboardingWatchdogNames.IsCapabilityProofName(r.GetValueOrDefault("name")))
               || _schedulers.Any(static r => OnboardingWatchdogNames.IsOnboardingWatchdogName(r.GetValueOrDefault("name"))
                                              || OnboardingWatchdogNames.IsCapabilityProofName(r.GetValueOrDefault("name")));

        public bool HasEnabledBootstrapAnchors
            => _filters.Any(static r =>
                r.GetValueOrDefault("comment")?.StartsWith("mfc:anchor:v1:", StringComparison.Ordinal) == true
                && r.GetValueOrDefault("disabled") is not ("yes" or "true" or "1"));

        public bool HasBootstrapRoots
            => _filters.Any(static r =>
                string.Equals(r.GetValueOrDefault("comment"), BootstrapArtifact.ReturnComment, StringComparison.Ordinal));

        public IReadOnlyList<string> UserComments
            => _filters
                .Select(static r => r.GetValueOrDefault("comment"))
                .Where(static c => c is not null && c.StartsWith("user-", StringComparison.Ordinal))
                .Select(static c => c!)
                .ToArray();

        public IReadOnlyList<string> StagedDisabledMarkers
            => _filters
                .Where(static r => r.GetValueOrDefault("comment")?.StartsWith("mfc:anchor:v1:", StringComparison.Ordinal) == true)
                .Select(static r => r["comment"])
                .ToArray();

        public string[] ScriptNames() => _scripts.Select(static r => r["name"]).ToArray();

        public string[] SchedulerNames() => _schedulers.Select(static r => r["name"]).ToArray();

        public Dictionary<string, bool> SchedulerDisabledMap()
            => _schedulers.ToDictionary(
                static r => r["name"],
                static r => r.GetValueOrDefault("disabled") is "yes" or "true" or "1",
                StringComparer.Ordinal);

        public static CombinedChannel WithUnmanagedBuiltins()
        {
            CombinedChannel channel = new();
            channel.SeedUnmanaged("input");
            channel.SeedUnmanaged("forward");
            channel.SeedUnmanaged("output");
            return channel;
        }

        public void SeedBootstrapRootCollision()
        {
            Dictionary<string, string> row = new(StringComparer.Ordinal)
            {
                [".id"] = NextId(),
                ["chain"] = BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
                ["action"] = "return",
                ["disabled"] = "no",
                ["comment"] = BootstrapArtifact.ReturnComment,
            };
            _filters.Add(row);
            _initial.Add(ToRule(row, 0));
        }

        public void SeedWatchdogResidue()
        {
            _scripts.Add(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [".id"] = NextId(),
                ["name"] = "mfc-ob-s-deadbeefdeadbeef",
                ["source"] = "# leftover",
            });
        }

        public void SeedExactAnchor(AnchorKey key, bool disabled, string? jumpTarget = null)
        {
            Dictionary<string, string> row = new(StringComparer.Ordinal)
            {
                [".id"] = NextId(),
                ["chain"] = key.Chain switch
                {
                    FilterBuiltInContext.Input => "input",
                    FilterBuiltInContext.Forward => "forward",
                    FilterBuiltInContext.Output => "output",
                    _ => throw new InvalidOperationException(key.Chain.ToString()),
                },
                ["action"] = "jump",
                ["jump-target"] = jumpTarget ?? BootstrapArtifact.RootChainName(key.Family, key.Chain),
                ["disabled"] = disabled ? "yes" : "no",
                ["comment"] = key.Marker,
            };
            _filters.Add(row);
        }

        public void SeedBootstrapReturn(AnchorKey key)
        {
            Dictionary<string, string> row = new(StringComparer.Ordinal)
            {
                [".id"] = NextId(),
                ["chain"] = BootstrapArtifact.RootChainName(key.Family, key.Chain),
                ["action"] = "return",
                ["disabled"] = "no",
                ["comment"] = BootstrapArtifact.ReturnComment,
            };
            _filters.Add(row);
        }

        public void SeedWatchdog(OnboardingOperationId operationId, DeviceId deviceId, bool disabled)
        {
            string token = OnboardingWatchdogNames.Token(operationId, deviceId);
            _scripts.Add(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [".id"] = NextId(),
                ["name"] = OnboardingWatchdogNames.RollbackScript(token),
                ["source"] = "# watchdog",
            });
            foreach (string name in new[]
                     {
                         OnboardingWatchdogNames.DeadlineScheduler(token),
                         OnboardingWatchdogNames.StartupScheduler(token),
                     })
            {
                _schedulers.Add(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [".id"] = NextId(),
                    ["name"] = name,
                    ["disabled"] = disabled ? "yes" : "no",
                    ["on-event"] = OnboardingWatchdogNames.RollbackScript(token),
                });
            }
        }

        public List<ActualFilterRule> ToFilterRules()
        {
            List<ActualFilterRule> rules = [];
            foreach (IGrouping<string, Dictionary<string, string>> group in _filters.GroupBy(static r => r["chain"], StringComparer.OrdinalIgnoreCase))
            {
                int ordinal = 0;
                foreach (Dictionary<string, string> row in group)
                {
                    rules.Add(ToRule(row, ordinal++));
                }
            }

            return rules;
        }

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            OnboardingWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            string fixedPath = OnboardingWritePaths.Fixed(path);
            if (fixedPath.Contains("/firewall/filter/add", StringComparison.Ordinal))
            {
                Dictionary<string, string> row = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
                row[".id"] = NextId();
                row.Remove("place-before");
                _filters.Add(row);
            }
            else if (fixedPath.Contains("/firewall/filter/set", StringComparison.Ordinal))
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                Dictionary<string, string> row = _filters.Single(r => r[".id"] == id);
                foreach (KeyValuePair<string, string> pair in attributes.Where(static a => a.Key != ".id"))
                {
                    row[pair.Key] = pair.Value;
                }
            }
            else if (fixedPath.Contains("/firewall/filter/remove", StringComparison.Ordinal))
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                _filters.RemoveAll(r => r[".id"] == id);
            }
            else if (fixedPath == "/system/script/add")
            {
                Dictionary<string, string> row = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
                row[".id"] = NextId();
                _scripts.Add(row);
            }
            else if (fixedPath == "/system/scheduler/add")
            {
                Dictionary<string, string> row = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
                row[".id"] = NextId();
                row["run-count"] = "1";
                _schedulers.Add(row);
            }
            else if (fixedPath == "/system/scheduler/set")
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                Dictionary<string, string> row = _schedulers.Single(r => r[".id"] == id);
                foreach (KeyValuePair<string, string> pair in attributes.Where(static a => a.Key != ".id"))
                {
                    row[pair.Key] = pair.Value;
                }
            }
            else if (fixedPath == "/system/script/remove")
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                _scripts.RemoveAll(r => r[".id"] == id);
            }
            else if (fixedPath == "/system/scheduler/remove")
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                _schedulers.RemoveAll(r => r[".id"] == id);
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["ok"] = "true" });
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
            IpAddressFamily family,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<IReadOnlyDictionary<string, string>> copy = _filters
                .Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                .ToArray();
            return Task.FromResult(copy);
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintSystemAsync(
            OnboardingSystemSurface surface,
            CancellationToken cancellationToken = default)
        {
            List<Dictionary<string, string>> rows = surface == OnboardingSystemSurface.Script ? _scripts : _schedulers;
            IReadOnlyList<IReadOnlyDictionary<string, string>> copy = rows
                .Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                .ToArray();
            return Task.FromResult(copy);
        }

        private void SeedUnmanaged(string chain)
        {
            Dictionary<string, string> row = new(StringComparer.Ordinal)
            {
                [".id"] = NextId(),
                ["chain"] = chain,
                ["action"] = "accept",
                ["disabled"] = "no",
                ["comment"] = $"user-{chain}",
            };
            _filters.Add(row);
            _initial.Add(ToRule(row, 0));
        }

        private static ActualFilterRule ToRule(Dictionary<string, string> row, int ordinal)
        {
            bool disabled = row.GetValueOrDefault("disabled") is "yes" or "true" or "1";
            return ActualFilterRule.Create(
                IpAddressFamily.IPv4,
                row["chain"],
                ordinal,
                row.GetValueOrDefault("action"),
                disabled,
                jumpTarget: row.GetValueOrDefault("jump-target"),
                comment: row.GetValueOrDefault("comment"));
        }

        private string NextId() => string.Create(CultureInfo.InvariantCulture, $"*{_nextId++}");
    }

    private sealed class ScriptedBootstrap : IOnboardingBootstrapWritePort
    {
        private readonly IOnboardingBootstrapWritePort _inner;

        public ScriptedBootstrap(IOnboardingBootstrapWritePort inner, bool failAdd = false, bool failEnable = false)
        {
            _inner = inner;
            FailAdd = failAdd;
            FailEnable = failEnable;
        }

        public bool FailAdd { get; }

        public bool FailEnable { get; }

        public Task<OnboardingBootstrapWriteExecutionResult> ApplyAsync(
            OnboardingBootstrapWrite write,
            IReadOnlyList<ActualFilterRule> liveSnapshot,
            CancellationToken cancellationToken = default)
        {
            if (FailAdd && write.Kind == OnboardingBootstrapWriteKind.AddBootstrapReturn)
            {
                return Task.FromResult(new OnboardingBootstrapWriteExecutionResult
                {
                    Succeeded = false,
                    Path = "/ip/firewall/filter/add",
                    SentAttributes = write.Attributes,
                    ReadBack = new Dictionary<string, string>(StringComparer.Ordinal),
                    Error = "forced add failure",
                });
            }

            if (FailEnable && write.Kind == OnboardingBootstrapWriteKind.SetAnchorDisabled)
            {
                return Task.FromResult(new OnboardingBootstrapWriteExecutionResult
                {
                    Succeeded = true,
                    Path = "/ip/firewall/filter/set",
                    SentAttributes = write.Attributes,
                    ReadBack = new Dictionary<string, string>(StringComparer.Ordinal) { ["disabled"] = "yes" },
                });
            }

            return _inner.ApplyAsync(write, liveSnapshot, cancellationToken);
        }
    }

    private sealed class ScriptedWatchdog : IOnboardingWatchdogPort
    {
        private readonly IOnboardingWatchdogPort _inner;

        public ScriptedWatchdog(IOnboardingWatchdogPort inner, bool failArm = false, bool failDisarm = false)
        {
            _inner = inner;
            FailArm = failArm;
            FailDisarm = failDisarm;
        }

        public bool FailArm { get; }

        public bool FailDisarm { get; }

        public Task<OnboardingWatchdogExecutionResult> ProveSchedulerAsync(
            SchedulerProofPlan plan,
            DateTimeOffset routerClock,
            CancellationToken cancellationToken = default)
            => _inner.ProveSchedulerAsync(plan, routerClock, cancellationToken);

        public Task<OnboardingWatchdogExecutionResult> ArmWatchdogAsync(
            OnboardingWatchdogBundle bundle,
            DateTimeOffset routerClock,
            TimeSpan? remainingTtl = null,
            CancellationToken cancellationToken = default)
        {
            if (FailArm)
            {
                return Task.FromResult(Fail(OnboardingCodes.OnboardingWatchdogArmFailed));
            }

            return _inner.ArmWatchdogAsync(bundle, routerClock, remainingTtl, cancellationToken);
        }

        public Task<OnboardingWatchdogExecutionResult> DisarmWatchdogAsync(
            OnboardingWatchdogBundle bundle,
            TimeSpan? remainingTtl = null,
            CancellationToken cancellationToken = default)
        {
            if (FailDisarm)
            {
                return Task.FromResult(Fail(OnboardingCodes.OnboardingWatchdogDisableFailed));
            }

            return _inner.DisarmWatchdogAsync(bundle, remainingTtl, cancellationToken);
        }

        public Task<OnboardingWatchdogExecutionResult> CleanupWatchdogAsync(
            OnboardingOperationId operationId,
            DeviceId deviceId,
            CancellationToken cancellationToken = default)
            => _inner.CleanupWatchdogAsync(operationId, deviceId, cancellationToken);

        private static OnboardingWatchdogExecutionResult Fail(string code)
            => new()
            {
                Succeeded = false,
                Code = code,
                Paths = [],
                SentAttributes = [],
                Error = code,
            };
    }
}
