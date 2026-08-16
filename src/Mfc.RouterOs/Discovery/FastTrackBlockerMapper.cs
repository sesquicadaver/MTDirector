using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps routing/filter/packet-path discovery onto Domain FastTrack analysis (M2-15).
/// Reuses topology-dependency facts; does not write FastTrack or compile the ACCEPT fallback pair.
/// <c>ipv4-fasttrack-active</c> is observation-only and does not enter the FastTrack context hash.
/// </summary>
public static class FastTrackBlockerMapper
{
    public static FastTrackAnalysisResult Analyze(
        IReadOnlyList<PolicyRule> rules,
        TopologyDependencyProfile profile,
        VrrpDiscoveryResult vrrp,
        RoutingDependencyDiscoveryResult routing,
        FirewallFilterDiscoveryResult? filter = null,
        PacketPathTopologyResult? packetPath = null,
        BridgeSwitchDiscoveryResult? bridge = null,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(vrrp);
        ArgumentNullException.ThrowIfNull(routing);
        TopologyDependencyFacts facts = TopologyDependencyBlockerMapper.FromDiscovery(profile, vrrp, routing, bridge);
        bool hasVrf = packetPath is not null
                      && packetPath.Nodes.Any(static n => n.Kind == PacketPathNodeKind.Vrf);
        bool preAnchor = filter is not null
                         && FastTrackAnalysis.HasPreAnchorUnmanagedFastTrack(
                             ActualFilterRuleMapper.FromDiscovery(filter));
        FastTrackTopologyContext topology = FastTrackTopologyContext.From(
            facts,
            hasVrf,
            preAnchor);
        return FastTrackAnalysis.Analyze(rules, topology, catalog);
    }
}
