using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class TopologyDependencyBlockerMapperTests
{
    [Fact]
    public void DiscoveryMapsVrrpSyncRawNatAndRpFilterWithoutWritingFacilities()
    {
        VrrpDiscoveryResult vrrp = VrrpDiscovery.BuildResult(
            Ok(
                RosReadCommandId.VrrpInterfaces,
                Row(
                    ("name", "vrrp1"),
                    ("interface", "ether1"),
                    ("vrid", "1"),
                    ("priority", "100"),
                    ("v3-protocol", "ipv4"),
                    ("sync-connection-tracking", "yes"),
                    ("connection-tracking-port", "8275"),
                    ("remote-address", "192.0.2.20"),
                    ("master", "true"),
                    ("backup", "false"),
                    ("running", "true"))));
        RoutingDependencyDiscoveryResult routing = RoutingDependencyDiscovery.BuildResult(
            Ok(RosReadCommandId.RoutingTables, Row(("name", "wan2"), ("fib", "yes"))),
            Ok(RosReadCommandId.RoutingRules, Row(("action", "lookup"), ("table", "wan2"), ("routing-mark", "wan2"))),
            Ok(RosReadCommandId.Ipv4StaticRoutes),
            Ok(RosReadCommandId.Ipv6StaticRoutes),
            Ok(RosReadCommandId.Ipv4DefaultRouteState, Row(("gateway", "1.1.1.1"), ("active", "true"))),
            Ok(RosReadCommandId.Ipv6DefaultRouteState),
            Ok(RosReadCommandId.Ipv4Nat),
            Ok(RosReadCommandId.Ipv6Nat),
            Ok(RosReadCommandId.Ipv4Raw, Row(("chain", "prerouting"), ("action", "notrack"))),
            Ok(RosReadCommandId.Ipv6Raw),
            Ok(
                RosReadCommandId.Ipv4Mangle,
                Row(
                    ("chain", "prerouting"),
                    ("action", "mark-routing"),
                    ("per-connection-classifier", "both-addresses:2/0"),
                    ("new-routing-mark", "wan1"))),
            Ok(RosReadCommandId.Ipv6Mangle),
            Ok(RosReadCommandId.Ipv4Settings, Row(("rp-filter", "strict"), ("ip-forward", "true"))),
            Ok(RosReadCommandId.Ipv6Settings, Row(("forward", "true"))));

        TopologyDependencyProfile profile = TopologyDependencyProfile.Create(
            NodeKind.Router,
            DeclaredUplinkMode.Failover,
            uplinks:
            [
                UplinkCoverageFact.Create("wan1", UplinkTrafficMode.Primary, "wan"),
                UplinkCoverageFact.Create("wan2", UplinkTrafficMode.Backup, zoneKey: null),
            ],
            declaredVrrpMemberIds: ["a", "b"],
            observedVrrpMemberIds: ["a"],
            observingDeviceId: "a",
            candidate: CandidatePolicySurface.Create(
                hasStatefulConnectionMatcher: true,
                hasDstNatMatcher: true));
        TopologyDependencyAnalysisResult result = TopologyDependencyBlockerMapper.Analyze(profile, vrrp, routing);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.VrrpMemberMissing);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.UplinkZoneCoverageMissing);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.StrictRpfWithVrrp);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.RawNotrackIntersectsStateful);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.ManglePccPresent);
        Assert.Contains(result.ProtectedFlows, f => f.Kind == VrrpProtectedFlowKind.Sync && f.DestinationPort == 8275);
        Assert.Equal(VrrpMemberObservedState.Master, Assert.Single(result.RoleVector).Role);
        Assert.False(result.HasCollapsedGlobalMaster);
    }

    [Fact]
    public void SwitchNodeWithoutProvenTransitBlocksForward()
    {
        VrrpDiscoveryResult vrrp = VrrpDiscovery.BuildResult(Ok(RosReadCommandId.VrrpInterfaces));
        RoutingDependencyDiscoveryResult routing = RoutingDependencyDiscovery.BuildResult(
            Ok(RosReadCommandId.RoutingTables),
            Ok(RosReadCommandId.RoutingRules),
            Ok(RosReadCommandId.Ipv4StaticRoutes),
            Ok(RosReadCommandId.Ipv6StaticRoutes),
            Ok(RosReadCommandId.Ipv4DefaultRouteState),
            Ok(RosReadCommandId.Ipv6DefaultRouteState),
            Ok(RosReadCommandId.Ipv4Nat),
            Ok(RosReadCommandId.Ipv6Nat),
            Ok(RosReadCommandId.Ipv4Raw),
            Ok(RosReadCommandId.Ipv6Raw),
            Ok(RosReadCommandId.Ipv4Mangle),
            Ok(RosReadCommandId.Ipv6Mangle),
            Ok(RosReadCommandId.Ipv4Settings, Row(("rp-filter", "no"))),
            Ok(RosReadCommandId.Ipv6Settings));
        BridgeSwitchDiscoveryResult bridge = BridgeSwitchDiscovery.BuildResult(
            Ok(RosReadCommandId.Bridges),
            Ok(RosReadCommandId.BridgePorts),
            Ok(RosReadCommandId.BridgeSettings),
            Ok(RosReadCommandId.BridgeVlans),
            Ok(RosReadCommandId.EthernetSwitches, Row(("name", "switch1"), ("type", "unknown"))),
            Ok(RosReadCommandId.EthernetSwitchPorts));
        TopologyDependencyProfile profile = TopologyDependencyProfile.Create(
            NodeKind.Switch,
            candidate: CandidatePolicySurface.Create(hasForward: true));
        TopologyDependencyAnalysisResult result = TopologyDependencyBlockerMapper.Analyze(
            profile,
            vrrp,
            routing,
            bridge);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.SwitchForwardPolicyUnsupported);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.SwitchHardwareProfileUnknown);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.SwitchTransitPathNotProven);
    }

    [Fact]
    public void RoleFlipDoesNotChangeMappedContextHash()
    {
        RoutingDependencyDiscoveryResult routing = RoutingDependencyDiscovery.BuildResult(
            Ok(RosReadCommandId.RoutingTables),
            Ok(RosReadCommandId.RoutingRules),
            Ok(RosReadCommandId.Ipv4StaticRoutes),
            Ok(RosReadCommandId.Ipv6StaticRoutes),
            Ok(RosReadCommandId.Ipv4DefaultRouteState, Row(("gateway", "1.1.1.1"), ("active", "true"))),
            Ok(RosReadCommandId.Ipv6DefaultRouteState),
            Ok(RosReadCommandId.Ipv4Nat),
            Ok(RosReadCommandId.Ipv6Nat),
            Ok(RosReadCommandId.Ipv4Raw),
            Ok(RosReadCommandId.Ipv6Raw),
            Ok(RosReadCommandId.Ipv4Mangle),
            Ok(RosReadCommandId.Ipv6Mangle),
            Ok(RosReadCommandId.Ipv4Settings, Row(("rp-filter", "no"))),
            Ok(RosReadCommandId.Ipv6Settings));
        TopologyDependencyProfile profile = TopologyDependencyProfile.Create(observingDeviceId: "a");
        TopologyDependencyAnalysisResult master = TopologyDependencyBlockerMapper.Analyze(
            profile,
            VrrpDiscovery.BuildResult(
                Ok(
                    RosReadCommandId.VrrpInterfaces,
                    Row(
                        ("name", "vrrp1"),
                        ("interface", "ether1"),
                        ("vrid", "1"),
                        ("master", "true"),
                        ("backup", "false"),
                        ("running", "true")))),
            routing);
        TopologyDependencyAnalysisResult backup = TopologyDependencyBlockerMapper.Analyze(
            profile,
            VrrpDiscovery.BuildResult(
                Ok(
                    RosReadCommandId.VrrpInterfaces,
                    Row(
                        ("name", "vrrp1"),
                        ("interface", "ether1"),
                        ("vrid", "1"),
                        ("master", "false"),
                        ("backup", "true"),
                        ("running", "true")))),
            routing);
        Assert.Equal(master.TopologyDependencyContextHash.ToString(), backup.TopologyDependencyContextHash.ToString());
        Assert.NotEqual(master.TopologyObservationHash.ToString(), backup.TopologyObservationHash.ToString());
    }

    private static RosReadCommandResult Ok(RosReadCommandId id, params RosReadRecord[] rows)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = rows,
            SessionInvalidated = false,
            Error = null,
        };

    private static RosReadRecord Row(params (string Name, string Value)[] properties)
    {
        Dictionary<string, string> known = new(StringComparer.Ordinal);
        foreach ((string name, string value) in properties)
        {
            known[name] = value;
        }

        return new RosReadRecord
        {
            KnownProperties = known,
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
