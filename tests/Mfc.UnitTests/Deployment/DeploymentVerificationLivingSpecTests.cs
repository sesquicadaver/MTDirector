using System.Globalization;
using System.Net;
using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Deployment;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-07 AC 1–11 (Safe Deployment Spec §32–§34).
/// </summary>
public sealed class DeploymentVerificationLivingSpecTests
{
    [Fact]
    public async Task Ac1ManagedResourceHashIsVerified()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        DeploymentVerificationResult mismatch = await VerifyAsync(
            plan,
            observedHash: DeploymentTestFactory.H("wrong"),
            seedAnchorsToNew: true);
        Assert.False(mismatch.Succeeded);
        Assert.True(mismatch.RequiresRollback);
        Assert.Equal(DeploymentCodes.ActiveArtifactHashMismatch, mismatch.Code);

        DeploymentVerificationResult ok = await VerifyAsync(
            plan,
            observedHash: plan.NewArtifactHash,
            seedAnchorsToNew: true);
        Assert.True(ok.Succeeded, ok.Message);
    }

    [Fact]
    public async Task Ac2ActiveAnchorTargetsAreVerified()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        DeploymentVerificationResult result = await VerifyAsync(
            plan,
            observedHash: plan.NewArtifactHash,
            seedAnchorsToNew: false);
        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRollback);
        Assert.Contains(result.Findings, static f => f.Code is DeploymentCodes.ActiveArtifactHashMismatch or DeploymentCodes.AnchorInvalid);
    }

    [Fact]
    public async Task Ac3OpensNewApiSslConnection()
    {
        DeviceDeploymentPlan plan = WithProbes(
            DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router),
            new DeploymentProbe(DeploymentProbeKind.ApiSsl, "10.0.0.1", 500));
        DeploymentWatchdogBundle watchdog = FakeWatchdog(plan.DeviceId);
        FakeFreshFactory factory = new(SeedChannel(plan, toNew: true, watchdog));
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

    [Fact]
    public async Task Ac4EstablishedSessionIsNotSufficient()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        DeploymentWatchdogBundle watchdog = FakeWatchdog(plan.DeviceId);
        RecordingChannel channel = SeedChannel(plan, toNew: true, watchdog);
        RouterOsDeploymentSession same = new(channel);
        FakeFreshFactory factory = new(channel, reuse: same);
        DeploymentVerificationResult result = await VerifyDeploymentActivationUseCase.ExecuteAsync(
            plan,
            priorSessionIdentity: same,
            factory,
            plan.NewArtifactHash,
            watchdog,
            TimeSpan.FromSeconds(120));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.ManagementReconnectFailed, result.Code);
        Assert.True(result.RequiresRollback);
    }

    [Fact]
    public void Ac5OnlyApiSslAndRouterPingAreSupported()
    {
        DeploymentProbe ping = new(DeploymentProbeKind.RouterPing, "192.0.2.1", 500);
        DeploymentProbe api = new(DeploymentProbeKind.ApiSsl, "10.0.0.1", 500);
        Assert.Equal(2, Enum.GetValues<DeploymentProbeKind>().Length);
        Assert.Contains(Enum.GetValues<DeploymentProbeKind>(), static k => k == DeploymentProbeKind.ApiSsl);
        Assert.Contains(Enum.GetValues<DeploymentProbeKind>(), static k => k == DeploymentProbeKind.RouterPing);
        ManagedIntegrityResult ok = PostActivationVerification.ValidateProbeProfile([ping, api]);
        Assert.True(ok.Passed);
    }

    [Fact]
    public void Ac6PingDoesNotAcceptHostname()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            new DeploymentProbe(DeploymentProbeKind.RouterPing, "router.example.com", 500));
        Assert.Contains(DeploymentCodes.ProbeHostnameForbidden, ex.Message, StringComparison.Ordinal);
        Assert.False(DeploymentProbe.TryParseLiteralIp("gw.local", out _));
    }

    [Fact]
    public void Ac7CountIntervalAndTimeoutAreBounded()
    {
        DeploymentProbe probe = new(DeploymentProbeKind.RouterPing, "192.0.2.1", 1000);
        Assert.Equal(3, DeploymentProbe.FixedPingCount);
        Assert.Throws<DomainInvariantException>(() =>
            new DeploymentProbe(DeploymentProbeKind.RouterPing, "192.0.2.1", 10));
        Assert.Throws<DomainInvariantException>(() =>
            new DeploymentProbe(DeploymentProbeKind.RouterPing, "192.0.2.1", 9000));
        RouterPingRequest request = new(IPAddress.Parse("192.0.2.1"), IpAddressFamily.IPv4, 500);
        Assert.Equal(3, request.Count);
        Assert.InRange(request.TimeoutMilliseconds, RouterPingRequest.MinTimeoutMs, RouterPingRequest.MaxTimeoutMs);
        Assert.Equal(1000, probe.TimeoutMilliseconds);
    }

    [Fact]
    public void Ac8SourceAddressTableAndInterfaceAreTyped()
    {
        DeploymentProbe probe = new(
            DeploymentProbeKind.RouterPing,
            "192.0.2.10",
            500,
            sourceAddress: "192.0.2.1",
            routingTable: "to-wan1",
            @interface: "ether1");
        Assert.Equal("192.0.2.1", probe.SourceAddress);
        Assert.Equal("to-wan1", probe.RoutingTable);
        Assert.Equal("ether1", probe.Interface);
        Assert.Throws<DomainInvariantException>(() =>
            new DeploymentProbe(
                DeploymentProbeKind.RouterPing,
                "192.0.2.10",
                500,
                sourceAddress: "src.example"));
    }

    [Fact]
    public async Task Ac9CriticalFailOrInconclusiveTriggersRollback()
    {
        Assert.NotNull(PostActivationVerification.ClassifyCriticalProbeOutcome(
            DeploymentProbeKind.RouterPing, "192.0.2.1", "FAIL"));
        Assert.NotNull(PostActivationVerification.ClassifyCriticalProbeOutcome(
            DeploymentProbeKind.RouterPing, "192.0.2.1", "INCONCLUSIVE"));
        Assert.Null(PostActivationVerification.ClassifyCriticalProbeOutcome(
            DeploymentProbeKind.RouterPing, "192.0.2.1", "PASS"));

        DeviceDeploymentPlan plan = WithProbes(
            DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router),
            new DeploymentProbe(DeploymentProbeKind.RouterPing, "192.0.2.1", 500));
        DeploymentWatchdogBundle watchdog = FakeWatchdog(plan.DeviceId);
        RecordingChannel channel = SeedChannel(plan, toNew: true, watchdog);
        channel.NextPing = new ChannelPingResult { Sent = 3, Received = 0 };
        DeploymentVerificationResult result = await VerifyDeploymentActivationUseCase.ExecuteAsync(
            plan,
            null,
            new FakeFreshFactory(channel),
            plan.NewArtifactHash,
            watchdog,
            TimeSpan.FromSeconds(120));
        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRollback);
        Assert.Equal(DeploymentCodes.DeploymentProbeFailed, result.Code);
    }

    [Fact]
    public void Ac10ProbeProfileIsPartOfPlanHash()
    {
        Node node = DeploymentTestFactory.RouterWithDevice(out Device device);
        DeploymentPlan a = DeploymentTestFactory.PlanFor(node);
        DeviceDeploymentPlan slice = a.DevicePlans.Single(p => p.DeviceId == device.Id);
        DeviceDeploymentPlan withExtra = DeviceDeploymentPlan.Create(
            slice.DeviceId,
            slice.ExpectedRouterOsVersion,
            slice.ExpectedCapabilityHash,
            slice.ExpectedConfigurationHash,
            slice.ExpectedCompatibilityHash,
            slice.ExpectedGuardContextHash,
            slice.ExpectedAnchorContextHash,
            slice.OldArtifactHash,
            slice.OldAnchorTargets,
            slice.NewArtifactHash,
            slice.NewAnchorTargets,
            slice.AnchorActivationOrder,
            slice.AnchorRollbackOrder,
            slice.TransitionStateHashes,
            slice.RollbackTtl,
            [
                new DeploymentProbe(DeploymentProbeKind.RouterPing, "192.0.2.1", 500),
                new DeploymentProbe(DeploymentProbeKind.ApiSsl, "10.0.0.1", 500),
            ]);
        DeploymentPlan b = DeploymentPlan.Create(
            node,
            a.LogicalPolicyHash,
            a.AnalysisBundleHash,
            a.TopologyProjectionHash,
            [withExtra],
            a.CreatedBy,
            a.CreatedAtUtc);
        Assert.False(a.PlanHash.Equals(b.PlanHash));
    }

    [Fact]
    public async Task Ac11WatchdogReadinessIsCheckedBeforeCommit()
    {
        ManagedIntegrityResult tooClose = PostActivationVerification.VerifyWatchdogReadiness(
            TimeSpan.FromSeconds(10),
            deadlineSchedulerPresent: true,
            deadlineSchedulerEnabled: true,
            startupSchedulerPresent: true);
        Assert.True(tooClose.RequiresRollback);
        Assert.Contains(tooClose.Findings, static f => f.Code == DeploymentCodes.WatchdogDeadlineTooClose);

        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        DeploymentWatchdogBundle watchdog = FakeWatchdog(plan.DeviceId);
        RecordingChannel channel = SeedChannel(plan, toNew: true, watchdog, includeWatchdog: false);
        DeploymentVerificationResult result = await VerifyDeploymentActivationUseCase.ExecuteAsync(
            plan,
            null,
            new FakeFreshFactory(channel),
            plan.NewArtifactHash,
            watchdog,
            TimeSpan.FromSeconds(120));
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.WatchdogNotReady, result.Code);
        Assert.True(result.RequiresRollback);
    }

    private static async Task<DeploymentVerificationResult> VerifyAsync(
        DeviceDeploymentPlan plan,
        Hash256 observedHash,
        bool seedAnchorsToNew)
    {
        DeploymentWatchdogBundle watchdog = FakeWatchdog(plan.DeviceId);
        RecordingChannel channel = SeedChannel(plan, toNew: seedAnchorsToNew, watchdog);
        return await VerifyDeploymentActivationUseCase.ExecuteAsync(
            plan,
            null,
            new FakeFreshFactory(channel),
            observedHash,
            watchdog,
            TimeSpan.FromSeconds(120));
    }

    private static DeviceDeploymentPlan WithProbes(DeviceDeploymentPlan basePlan, params DeploymentProbe[] probes)
        => DeviceDeploymentPlan.Create(
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
            probes);

    private static DeploymentWatchdogBundle FakeWatchdog(DeviceId deviceId)
    {
        DeploymentOperationId id = DeploymentOperationId.New();
        string token = DeploymentWatchdogNames.Token(id, deviceId);
        return new DeploymentWatchdogBundle
        {
            Token = token,
            DeviceId = deviceId,
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
    }

    private static RecordingChannel SeedChannel(
        DeviceDeploymentPlan plan,
        bool toNew,
        DeploymentWatchdogBundle? watchdog = null,
        bool includeWatchdog = true)
    {
        RecordingChannel channel = new();
        int id = 1;
        IReadOnlyList<AnchorTarget> targets = toNew ? plan.NewAnchorTargets : plan.OldAnchorTargets;
        foreach (AnchorTarget target in targets)
        {
            string chain = target.Key.Chain switch
            {
                FilterBuiltInContext.Input => "input",
                FilterBuiltInContext.Forward => "forward",
                FilterBuiltInContext.Output => "output",
                _ => "input",
            };
            channel.Seed(
                DeploymentReadSurface.Ipv4Filter,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [".id"] = "*" + id.ToString(CultureInfo.InvariantCulture),
                    ["chain"] = chain,
                    ["action"] = "jump",
                    ["jump-target"] = target.JumpTarget,
                    ["comment"] = target.Key.Marker,
                    ["disabled"] = "false",
                });
            id++;
        }

        if (includeWatchdog)
        {
            DeploymentWatchdogBundle wd = watchdog ?? FakeWatchdog(plan.DeviceId);
            channel.Seed(
                DeploymentReadSurface.Scheduler,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [".id"] = "*d",
                    ["name"] = wd.DeadlineSchedulerName,
                    ["disabled"] = "false",
                });
            channel.Seed(
                DeploymentReadSurface.Scheduler,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [".id"] = "*b",
                    ["name"] = wd.StartupSchedulerName,
                    ["disabled"] = "false",
                });
        }

        return channel;
    }

    private sealed class FakeFreshFactory : IDeploymentFreshSessionFactory
    {
        private readonly RecordingChannel _channel;
        private readonly RouterOsDeploymentSession? _reuse;

        public FakeFreshFactory(RecordingChannel channel, RouterOsDeploymentSession? reuse = null)
        {
            _channel = channel;
            _reuse = reuse;
        }

        public int OpenCount { get; private set; }

        public Task<IRouterOsDeploymentSession> OpenFreshAsync(CancellationToken cancellationToken = default)
        {
            OpenCount++;
            if (_reuse is not null)
            {
                return Task.FromResult<IRouterOsDeploymentSession>(_reuse);
            }

            return Task.FromResult<IRouterOsDeploymentSession>(new RouterOsDeploymentSession(_channel));
        }
    }

    private sealed class RecordingChannel : IDeploymentWriteChannel
    {
        private readonly Dictionary<DeploymentReadSurface, List<Dictionary<string, string>>> _prints = new();

        public ChannelPingResult? NextPing { get; set; }

        public void Seed(DeploymentReadSurface surface, Dictionary<string, string> row)
        {
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                list = [];
                _prints[surface] = list;
            }

            list.Add(new Dictionary<string, string>(row, StringComparer.Ordinal));
        }

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            DeploymentWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal));

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
            DeploymentReadSurface surface,
            CancellationToken cancellationToken = default)
        {
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>([]);
            }

            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>(
                list.Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                    .ToArray());
        }

        public Task<ChannelPingResult> PingAsync(
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(NextPing ?? new ChannelPingResult { Sent = 3, Received = 3 });
    }
}
