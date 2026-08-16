using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class FastTrackBlockerMapperTests
{
    private static readonly ServiceObject Tcp = ServiceObject.Create(
        PolicyObjectOwnerScope.Company,
        null,
        null,
        NonEmptyName.Create("tcp"),
        [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "tcp"))]);

    [Fact]
    public void DiscoveryMapsSafeSingleWanWithoutWritingFastTrack()
    {
        FastTrackAnalysisResult result = FastTrackBlockerMapper.Analyze(
            [AllowedRule()],
            TopologyDependencyProfile.Create(uplinkMode: DeclaredUplinkMode.One),
            VrrpDiscovery.BuildResult(Ok(RosReadCommandId.VrrpInterfaces)),
            EmptyRouting(),
            EmptyFilter(),
            catalog: new Dictionary<ServiceObjectId, ServiceObject> { [Tcp.Id] = Tcp });
        Assert.True(result.AllowsSafeFastTrack);
        Assert.True(result.RequiresAcceptFallback);
        Assert.Equal(FastTrackAnalysisCodes.RiskHigh, result.RiskFloor);
    }

    [Fact]
    public void DiscoveryPccPreAnchorAndVrfBlockFastTrackAndFasttrackActiveIsObservationOnly()
    {
        RoutingDependencyDiscoveryResult withPcc = RoutingDependencyDiscovery.BuildResult(
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
            Ok(
                RosReadCommandId.Ipv4Mangle,
                Row(
                    ("chain", "prerouting"),
                    ("action", "mark-routing"),
                    ("per-connection-classifier", "both-addresses:2/0"),
                    ("new-routing-mark", "wan1"))),
            Ok(RosReadCommandId.Ipv6Mangle),
            Ok(RosReadCommandId.Ipv4Settings, Row(("ipv4-fasttrack-active", "true"))),
            Ok(RosReadCommandId.Ipv6Settings));
        RoutingDependencyDiscoveryResult inactive = RoutingDependencyDiscovery.BuildResult(
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
            Ok(
                RosReadCommandId.Ipv4Mangle,
                Row(
                    ("chain", "prerouting"),
                    ("action", "mark-routing"),
                    ("per-connection-classifier", "both-addresses:2/0"),
                    ("new-routing-mark", "wan1"))),
            Ok(RosReadCommandId.Ipv6Mangle),
            Ok(RosReadCommandId.Ipv4Settings, Row(("ipv4-fasttrack-active", "false"))),
            Ok(RosReadCommandId.Ipv6Settings));
        FirewallFilterDiscoveryResult filter = FirewallFilterDiscovery.BuildResult(
            Ok(
                RosReadCommandId.Ipv4Filter,
                Row(("chain", "forward"), ("action", "fasttrack-connection"), ("comment", "unmanaged")),
                Row(("chain", "forward"), ("action", "jump"), ("comment", "fwc:anchor:forward"))),
            Ok(RosReadCommandId.Ipv6Filter),
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));
        PacketPathTopologyResult packetPath = new()
        {
            Nodes =
            [
                new PacketPathTopologyNode
                {
                    Kind = PacketPathNodeKind.Vrf,
                    Key = "vrf:containers",
                    Name = "containers",
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal),
                },
            ],
            Edges = [],
            Findings = [],
            Warnings = [],
            SharedVethNames = [],
            AssumesBridgeTrafficPassesIpFirewall = false,
        };
        TopologyDependencyProfile profile = TopologyDependencyProfile.Create(uplinkMode: DeclaredUplinkMode.One);
        PolicyRule rule = AllowedRule();
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> catalog =
            new Dictionary<ServiceObjectId, ServiceObject> { [Tcp.Id] = Tcp };
        FastTrackAnalysisResult blocked = FastTrackBlockerMapper.Analyze(
            [rule],
            profile,
            VrrpDiscovery.BuildResult(Ok(RosReadCommandId.VrrpInterfaces)),
            withPcc,
            filter,
            packetPath,
            catalog: catalog);
        FastTrackAnalysisResult inactiveFt = FastTrackBlockerMapper.Analyze(
            [rule],
            profile,
            VrrpDiscovery.BuildResult(Ok(RosReadCommandId.VrrpInterfaces)),
            inactive,
            filter,
            packetPath,
            catalog: catalog);
        Assert.Contains(blocked.Findings, f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(blocked.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorFasttrackBypasses);
        Assert.Equal(blocked.FastTrackContextHash.ToString(), inactiveFt.FastTrackContextHash.ToString());
        Assert.Equal("true", withPcc.Ipv4Settings.Ipv4FasttrackActive);
        Assert.Equal("false", inactive.Ipv4Settings.Ipv4FasttrackActive);
    }

    private static PolicyRule AllowedRule()
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            0,
            TrafficPredicate.Create(
                services: ServiceSelector.Create([Tcp.Id]),
                connectionStates: [ConnectionState.Established, ConnectionState.Related],
                serviceCatalog: new Dictionary<ServiceObjectId, ServiceObject> { [Tcp.Id] = Tcp }),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept));

    private static RoutingDependencyDiscoveryResult EmptyRouting()
        => RoutingDependencyDiscovery.BuildResult(
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
            Ok(RosReadCommandId.Ipv4Settings),
            Ok(RosReadCommandId.Ipv6Settings));

    private static FirewallFilterDiscoveryResult EmptyFilter()
        => FirewallFilterDiscovery.BuildResult(
            Ok(RosReadCommandId.Ipv4Filter),
            Ok(RosReadCommandId.Ipv6Filter),
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));

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
