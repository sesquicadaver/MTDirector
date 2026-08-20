using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-09 AC 1–10 (Safe Deployment Spec §36).
/// </summary>
public sealed class MultiWanDeploymentVerificationLivingSpecTests
{
    [Fact]
    public void Ac1RoutingNatRawMangleHashesAreRechecked()
    {
        MultiWanDependencyHashes expected = Hashes("base");
        MultiWanDependencyHashes drifted = Hashes("base") with { NatHash = DeploymentTestFactory.H("nat-drift") };
        ManagedIntegrityResult result = MultiWanDeploymentVerification.RecheckDependencyHashes(expected, drifted);
        Assert.False(result.Passed);
        Assert.Contains(result.Findings, static f => f.Code == DeploymentCodes.MultiWanDependencyDrift && f.Target == "nat");
        Assert.True(result.RequiresRollback);
    }

    [Fact]
    public void Ac2ZoneAndInterfaceListDependenciesAreRechecked()
    {
        MultiWanDependencyHashes expected = Hashes("z");
        MultiWanDependencyHashes drifted = Hashes("z") with
        {
            ZoneResolutionHash = DeploymentTestFactory.H("zone-new"),
            InterfaceListMembershipHash = DeploymentTestFactory.H("il-new"),
        };
        ManagedIntegrityResult result = MultiWanDeploymentVerification.RecheckDependencyHashes(expected, drifted);
        Assert.False(result.Passed);
        Assert.Contains(result.Findings, static f => f.Target == "zone");
        Assert.Contains(result.Findings, static f => f.Target == "interface-list");
    }

    [Fact]
    public void Ac3ActiveRouteStateDoesNotChangeArtifact()
    {
        Hash256 artifact = DeploymentTestFactory.H("artifact");
        Hash256 routeA = DeploymentTestFactory.H("route-primary");
        Hash256 routeB = DeploymentTestFactory.H("route-backup");
        Assert.NotEqual(routeA, routeB);
        Hash256 viaA = MultiWanDeploymentVerification.ArtifactHashIgnoringActiveRoute(artifact, routeA);
        Hash256 viaB = MultiWanDeploymentVerification.ArtifactHashIgnoringActiveRoute(artifact, routeB);
        Assert.Equal(artifact, viaA);
        Assert.Equal(viaA, viaB);
    }

    [Fact]
    public void Ac4PerTablePingRequiredForBalanced()
    {
        MultiWanUplinkTopology topology = new()
        {
            UplinkMode = DeclaredUplinkMode.Balanced,
            RequiredRoutingTables = ["wan1", "wan2"],
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };
        DeploymentProbe[] probes =
        [
            new(DeploymentProbeKind.RouterPing, "192.0.2.1", 500, routingTable: "wan1"),
            new(DeploymentProbeKind.RouterPing, "192.0.2.2", 500, routingTable: "wan2"),
        ];
        ManagedIntegrityResult ok = MultiWanDeploymentVerification.PlanRuntimeProbes(topology, probes, out IReadOnlyList<DeploymentProbe> selected);
        Assert.True(ok.Passed, string.Join(';', ok.Findings.Select(static f => f.Message)));
        Assert.Equal(2, selected.Count);

        ManagedIntegrityResult missing = MultiWanDeploymentVerification.PlanRuntimeProbes(
            topology,
            [probes[0]],
            out _);
        Assert.False(missing.Passed);
        Assert.Equal(DeploymentCodes.MultiWanProbeCoverageMissing, missing.Findings[0].Code);
    }

    [Fact]
    public void Ac5CurrentActivePathCheckedForFailover()
    {
        MultiWanUplinkTopology topology = new()
        {
            UplinkMode = DeclaredUplinkMode.Failover,
            RequiredRoutingTables = [],
            ActivePathDestination = "198.51.100.1",
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };
        DeploymentProbe[] probes =
        [
            new(DeploymentProbeKind.RouterPing, "198.51.100.1", 500),
            new(DeploymentProbeKind.RouterPing, "203.0.113.1", 500),
        ];
        ManagedIntegrityResult result = MultiWanDeploymentVerification.PlanRuntimeProbes(topology, probes, out IReadOnlyList<DeploymentProbe> selected);
        Assert.True(result.Passed);
        Assert.Single(selected);
        Assert.Equal("198.51.100.1", selected[0].Destination);
    }

    [Fact]
    public void Ac6ControllerDoesNotDisablePrimaryWan()
    {
        MultiWanUplinkTopology topology = Topology(DeclaredUplinkMode.Failover) with { DisablePrimaryWanRequested = true };
        ManagedIntegrityResult result = MultiWanDeploymentVerification.RejectForbiddenOperationalIntents(topology);
        Assert.False(result.Passed);
        Assert.Contains(result.Findings, static f => f.Target == "disable-primary");
    }

    [Fact]
    public void Ac7ControllerDoesNotCreateTemporaryRoute()
    {
        MultiWanUplinkTopology topology = Topology(DeclaredUplinkMode.Failover) with { TemporaryRouteRequested = true };
        ManagedIntegrityResult result = MultiWanDeploymentVerification.RejectForbiddenOperationalIntents(topology);
        Assert.False(result.Passed);
        Assert.Contains(result.Findings, static f => f.Target == "temporary-route");
    }

    [Fact]
    public void Ac8BackupPathNotTestedByForcedFailover()
    {
        MultiWanUplinkTopology topology = Topology(DeclaredUplinkMode.Failover) with { ForcedFailoverRequested = true };
        ManagedIntegrityResult result = MultiWanDeploymentVerification.PlanRuntimeProbes(
            topology,
            [new DeploymentProbe(DeploymentProbeKind.RouterPing, "198.51.100.1", 500)],
            out IReadOnlyList<DeploymentProbe> selected);
        Assert.False(result.Passed);
        Assert.Empty(selected);
        Assert.Equal(DeploymentCodes.MultiWanForcedFailoverForbidden, result.Findings[0].Code);
    }

    [Fact]
    public void Ac9DependencyChangeBlocksOrRollsBack()
    {
        MultiWanDependencyHashes expected = Hashes("plan");
        MultiWanDependencyHashes observed = Hashes("plan") with { RoutingConfigHash = DeploymentTestFactory.H("routing-changed") };
        ManagedIntegrityResult result = MultiWanDeploymentVerification.RecheckDependencyHashes(expected, observed);
        Assert.False(result.Passed);
        Assert.True(result.RequiresRollback);
        Assert.All(result.Findings, static f => Assert.True(f.RequiresRollback));
    }

    [Fact]
    public void Ac10ControllerDoesNotChangeRoutingNatMangle()
    {
        ManagedIntegrityResult clean = MultiWanDeploymentVerification.EnsureFilterOnlyWriteSurface(
        [
            "/ip/firewall/filter/add",
            "/ip/firewall/filter/set",
            "/system/script/add",
            "/system/scheduler/add",
        ]);
        Assert.True(clean.Passed);

        ManagedIntegrityResult dirty = MultiWanDeploymentVerification.EnsureFilterOnlyWriteSurface(
        [
            "/ip/firewall/filter/add",
            "/ip/route/add",
            "/ip/firewall/nat/add",
            "/ip/firewall/mangle/add",
        ]);
        Assert.False(dirty.Passed);
        Assert.Equal(3, dirty.Findings.Count);
        Assert.All(dirty.Findings, static f => Assert.Equal(DeploymentCodes.MultiWanWriteSurfaceViolation, f.Code));
    }

    [Fact]
    public async Task UseCaseSkipsNonMultiWanNodes()
    {
        MultiWanDeploymentVerificationResult result = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DeclaredUplinkMode.One,
            Hashes("x"),
            Hashes("x"),
            Topology(DeclaredUplinkMode.One),
            [],
            [],
            DeploymentTestFactory.H("art"),
            DeploymentTestFactory.H("route"));
        Assert.True(result.Succeeded);
        Assert.True(result.SkippedBecauseNotMultiWan);
    }

    [Fact]
    public async Task UseCaseExecutesBalancedPerTableProbes()
    {
        MultiWanDependencyHashes hashes = Hashes("ok");
        MultiWanUplinkTopology topology = new()
        {
            UplinkMode = DeclaredUplinkMode.Balanced,
            RequiredRoutingTables = ["t1", "t2"],
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };
        DeploymentProbe[] probes =
        [
            new(DeploymentProbeKind.RouterPing, "192.0.2.10", 500, routingTable: "t1"),
            new(DeploymentProbeKind.RouterPing, "192.0.2.11", 500, routingTable: "t2"),
        ];
        FakePingSession session = new();
        MultiWanDeploymentVerificationResult result = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DeclaredUplinkMode.Balanced,
            hashes,
            hashes,
            topology,
            probes,
            ["/ip/firewall/filter/add"],
            DeploymentTestFactory.H("art"),
            DeploymentTestFactory.H("route"),
            session);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.MultiWanProbeCount);
        Assert.Equal(2, session.PingCount);
    }

    [Fact]
    public async Task UseCaseRollsBackOnDependencyDrift()
    {
        MultiWanDeploymentVerificationResult result = await VerifyMultiWanDeploymentUseCase.ExecuteAsync(
            DeclaredUplinkMode.Failover,
            Hashes("plan"),
            Hashes("plan") with { MangleHash = DeploymentTestFactory.H("mangle-drift") },
            Topology(DeclaredUplinkMode.Failover) with { ActivePathDestination = "192.0.2.1" },
            [new DeploymentProbe(DeploymentProbeKind.RouterPing, "192.0.2.1", 500)],
            ["/ip/firewall/filter/add"],
            DeploymentTestFactory.H("art"),
            DeploymentTestFactory.H("route"));
        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRollback);
        Assert.Equal(DeploymentCodes.MultiWanDependencyDrift, result.Code);
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

    private static MultiWanUplinkTopology Topology(DeclaredUplinkMode mode)
        => new()
        {
            UplinkMode = mode,
            RequiredRoutingTables = [],
            ActivePathDestination = "192.0.2.1",
            ForcedFailoverRequested = false,
            DisablePrimaryWanRequested = false,
            TemporaryRouteRequested = false,
        };

    private sealed class FakePingSession : IRouterOsDeploymentSession
    {
        public int PingCount { get; private set; }

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
        {
            PingCount++;
            return Task.FromResult(new RouterPingResult
            {
                Outcome = RouterPingOutcome.Pass,
                Sent = 3,
                Received = 3,
            });
        }
    }
}
