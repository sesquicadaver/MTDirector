using System.Globalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps VRRP and routing-dependency discovery onto Domain topology-dependency analysis (M2-14).
/// Includes RAW notrack, NAT, Mangle PCC, rp-filter, and switch-chip facts that canonical sections may omit.
/// Does not write NAT/RAW/Mangle/VRRP and does not disable primary WAN.
/// </summary>
public static class TopologyDependencyBlockerMapper
{
    public static TopologyDependencyAnalysisResult Analyze(
        TopologyDependencyProfile profile,
        VrrpDiscoveryResult vrrp,
        RoutingDependencyDiscoveryResult routing,
        BridgeSwitchDiscoveryResult? bridge = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(vrrp);
        ArgumentNullException.ThrowIfNull(routing);
        return TopologyDependencyAnalysis.Analyze(FromDiscovery(profile, vrrp, routing, bridge));
    }

    public static TopologyDependencyFacts FromDiscovery(
        TopologyDependencyProfile profile,
        VrrpDiscoveryResult vrrp,
        RoutingDependencyDiscoveryResult routing,
        BridgeSwitchDiscoveryResult? bridge = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(vrrp);
        ArgumentNullException.ThrowIfNull(routing);
        List<VrrpInstanceFacts> instances = [];
        List<VrrpRoleAssignment> roles = [];
        foreach (VrrpInstanceDiscovery instance in vrrp.Instances)
        {
            IpAddressFamily family = MapFamily(instance.Family);
            ushort port = TopologyDependencyAnalysis.DefaultVrrpSyncPort;
            if (!string.IsNullOrWhiteSpace(instance.ConnectionTrackingPort)
                && ushort.TryParse(
                    instance.ConnectionTrackingPort,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ushort parsed)
                && parsed != 0)
            {
                port = parsed;
            }

            string iface = instance.ParentInterface ?? instance.GroupKey.InterfaceName;
            if (string.IsNullOrWhiteSpace(iface))
            {
                continue;
            }

            instances.Add(VrrpInstanceFacts.Create(
                family,
                instance.Vrid,
                iface,
                disabled: IsTruthy(instance.Disabled),
                syncConnectionTracking: IsTruthy(instance.SyncConnectionTracking),
                syncPort: port,
                remoteAddress: instance.RemoteAddress));
            roles.Add(VrrpRoleAssignment.Create(
                profile.ObservingDeviceId,
                family,
                instance.Vrid,
                iface,
                instance.DomainObservedState));
        }

        bool hardwareKnown = profile.SwitchHardwareProfileKnown;
        bool transitProven = profile.SwitchTransitPathProven;
        if (profile.Kind == NodeKind.Switch)
        {
            if (bridge is null)
            {
                hardwareKnown = false;
                transitProven = false;
            }
            else
            {
                hardwareKnown = bridge.EthernetSwitches.Count > 0
                                && bridge.EthernetSwitches.All(static s => s.HasKnownChipProfile)
                                && !bridge.PathRoleIndicators.Contains(BridgePathRoleIndicator.UnknownSwitchChip);
                transitProven = IsTruthy(bridge.BridgeSettings.UseIpFirewall)
                                && !bridge.PathRoleIndicators.Contains(BridgePathRoleIndicator.L3HardwareOffloadConfigured)
                                && !bridge.PathRoleIndicators.Contains(BridgePathRoleIndicator.UnknownSwitchChip)
                                && !bridge.PathRoleIndicators.Contains(BridgePathRoleIndicator.HardwareOffloadObserved);
            }
        }

        return TopologyDependencyFacts.Create(
            profile.Kind,
            profile.UplinkMode,
            profile.Uplinks,
            instances,
            profile.DeclaredVrrpMemberIds,
            profile.ObservedVrrpMemberIds,
            roles,
            routing.RoutingTables
                .Where(static t => !string.IsNullOrWhiteSpace(t.Name))
                .Select(static t => RoutingTableFact.Create(t.Name!, IsTruthy(t.Disabled)))
                .ToArray(),
            routing.RoutingRules
                .Select(static r => RoutingRuleFact.Create(
                    r.EffectiveOrdinal,
                    r.Action,
                    r.Table,
                    r.RoutingMark,
                    IsTruthy(r.Disabled) || r.IsDynamic))
                .ToArray(),
            routing.Ipv4Settings.RpFilter,
            MapFacility(routing.Ipv4RawRules).Concat(MapFacility(routing.Ipv6RawRules)).ToArray(),
            MapFacility(routing.Ipv4NatRules).Concat(MapFacility(routing.Ipv6NatRules)).ToArray(),
            MapFacility(routing.Ipv4MangleRules).Concat(MapFacility(routing.Ipv6MangleRules)).ToArray(),
            profile.Candidate,
            hardwareKnown,
            transitProven,
            routing.Ipv4DefaultRouteState
                .Select(static r => DefaultRouteObservation.Create(
                    MapFamily(r.Family),
                    r.RoutingTable,
                    r.Gateway,
                    r.Active,
                    r.GatewayStatus))
                .Concat(routing.Ipv6DefaultRouteState.Select(static r => DefaultRouteObservation.Create(
                    MapFamily(r.Family),
                    r.RoutingTable,
                    r.Gateway,
                    r.Active,
                    r.GatewayStatus)))
                .ToArray());
    }

    private static List<FacilityRuleFact> MapFacility(IReadOnlyList<OrderedFirewallFacilityRuleDiscovery> rules)
    {
        List<FacilityRuleFact> mapped = [];
        foreach (OrderedFirewallFacilityRuleDiscovery rule in rules)
        {
            mapped.Add(FacilityRuleFact.Create(
                MapFamily(rule.Family),
                rule.EffectiveOrdinal,
                rule.Chain,
                rule.Action,
                IsTruthy(rule.Disabled),
                rule.RoutingMark,
                rule.NewRoutingMark,
                Known(rule, "per-connection-classifier"),
                rule.ConnectionMark,
                rule.PacketMark,
                Known(rule, "new-connection-mark"),
                Known(rule, "new-packet-mark"),
                Known(rule, "to-addresses"),
                Known(rule, "to-ports"),
                Known(rule, "connection-state"),
                Known(rule, "connection-nat-state"),
                rule.UnsupportedMatchers));
        }

        return mapped;
    }

    private static IpAddressFamily MapFamily(IpAddressFamilyKind family)
        => family == IpAddressFamilyKind.Ipv6 ? IpAddressFamily.IPv6 : IpAddressFamily.IPv4;

    private static string? Known(OrderedFirewallFacilityRuleDiscovery rule, string key)
        => rule.KnownProperties.TryGetValue(key, out string? value) ? value : null;

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
