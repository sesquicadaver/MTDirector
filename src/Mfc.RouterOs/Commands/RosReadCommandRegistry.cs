using Mfc.RouterOs.Redaction;

namespace Mfc.RouterOs.Commands;

/// <summary>
/// Compile-time allowlist of RouterOS read commands (Read Adapter Spec §17).
/// Callers select commands by <see cref="RosReadCommandId"/> only — never by free-form path.
/// </summary>
public static class RosReadCommandRegistry
{
    private static readonly HashSet<string> ForbiddenPathSegments = new(StringComparer.Ordinal)
    {
        "add", "set", "remove", "enable", "disable", "move", "reset",
        "export", "import", "execute", "run", "login", "quit",
    };

    private static readonly Dictionary<RosReadCommandId, RosReadCommandDefinition> ById;

    static RosReadCommandRegistry()
    {
        RosReadCommandDefinition[] all =
        [
            Def(RosReadCommandId.SystemIdentity, "/system/identity/print", RosResultShape.Singleton, RosRequirement.Required, RosPassPolicy.BothPasses,
                Props("system_identity", P(".id"), P("name"))),
            Def(RosReadCommandId.SystemResource, "/system/resource/print", RosResultShape.Singleton, RosRequirement.Required, RosPassPolicy.StabilityGuard,
                Props("system_resource",
                    P("version", RosPropertyClassification.CapabilityTyped),
                    P("build-time", RosPropertyClassification.CapabilityTyped),
                    P("architecture-name", RosPropertyClassification.CapabilityTyped),
                    P("board-name", RosPropertyClassification.CapabilityTyped),
                    P("platform", RosPropertyClassification.CapabilityTyped),
                    P("uptime", RosPropertyClassification.ObservationTyped))),
            Def(RosReadCommandId.SystemRouterboard, "/system/routerboard/print", RosResultShape.Singleton, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                Props("system_routerboard",
                    P("routerboard", RosPropertyClassification.CapabilityTyped),
                    P("model", RosPropertyClassification.CapabilityTyped),
                    P("serial-number", RosPropertyClassification.CapabilityTyped, RosRedactionPolicy.LogRedacted),
                    P("firmware-type", RosPropertyClassification.CapabilityTyped),
                    P("factory-firmware", RosPropertyClassification.CapabilityTyped),
                    P("current-firmware", RosPropertyClassification.CapabilityTyped),
                    P("upgrade-firmware", RosPropertyClassification.CapabilityTyped))),
            Def(RosReadCommandId.SystemPackages, "/system/package/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                Props("system_packages",
                    P(".id", RosPropertyClassification.RawOnly),
                    P("name", RosPropertyClassification.CapabilityTyped),
                    P("version", RosPropertyClassification.CapabilityTyped),
                    P("build-time", RosPropertyClassification.CapabilityTyped),
                    P("scheduled", RosPropertyClassification.CapabilityTyped),
                    P("disabled", RosPropertyClassification.CapabilityTyped))),
            Def(RosReadCommandId.SystemClock, "/system/clock/print", RosResultShape.Singleton, RosRequirement.Required, RosPassPolicy.Pass1Only,
                Props("system_clock",
                    P("time", RosPropertyClassification.ObservationTyped),
                    P("date", RosPropertyClassification.ObservationTyped),
                    P("time-zone-name", RosPropertyClassification.ConfigTyped),
                    P("gmt-offset", RosPropertyClassification.ObservationTyped),
                    P("dst-active", RosPropertyClassification.ObservationTyped))),
            Def(RosReadCommandId.IpServices, "/ip/service/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                Props("ip_services",
                    P(".id", RosPropertyClassification.RawOnly),
                    P("name"),
                    P("port"),
                    P("address"),
                    P("certificate"),
                    P("tls-version"),
                    P("vrf"),
                    P("max-sessions"),
                    P("disabled"),
                    P("dynamic", RosPropertyClassification.ObservationTyped),
                    P("invalid", RosPropertyClassification.ObservationTyped))),
            Def(RosReadCommandId.Interfaces, "/interface/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                Props("interfaces",
                    P(".id", RosPropertyClassification.RawOnly),
                    P("name"),
                    P("default-name"),
                    P("type"),
                    P("mtu"),
                    P("mac-address"),
                    P("disabled"),
                    P("comment", redaction: RosRedactionPolicy.LogRedacted),
                    P("actual-mtu", RosPropertyClassification.ObservationTyped),
                    P("l2mtu", RosPropertyClassification.ObservationTyped),
                    P("max-l2mtu", RosPropertyClassification.ObservationTyped),
                    P("dynamic", RosPropertyClassification.ObservationTyped),
                    P("running", RosPropertyClassification.ObservationTyped),
                    P("slave", RosPropertyClassification.ObservationTyped),
                    P("invalid", RosPropertyClassification.ObservationTyped))),
            Def(RosReadCommandId.Ipv4Addresses, "/ip/address/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                Props("ipv4_addresses",
                    P(".id", RosPropertyClassification.RawOnly),
                    P("address"),
                    P("network"),
                    P("interface"),
                    P("disabled"),
                    P("comment", redaction: RosRedactionPolicy.LogRedacted),
                    P("actual-interface", RosPropertyClassification.ObservationTyped),
                    P("dynamic", RosPropertyClassification.ObservationTyped),
                    P("invalid", RosPropertyClassification.ObservationTyped),
                    P("slave", RosPropertyClassification.ObservationTyped))),
            Def(RosReadCommandId.Ipv6Addresses, "/ipv6/address/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                Props("ipv6_addresses",
                    P(".id", RosPropertyClassification.RawOnly),
                    P("address"),
                    P("from-pool"),
                    P("interface"),
                    P("advertise"),
                    P("eui-64"),
                    P("no-dad"),
                    P("disabled"),
                    P("comment", redaction: RosRedactionPolicy.LogRedacted),
                    P("actual-interface", RosPropertyClassification.ObservationTyped),
                    P("dynamic", RosPropertyClassification.ObservationTyped),
                    P("global", RosPropertyClassification.ObservationTyped),
                    P("invalid", RosPropertyClassification.ObservationTyped),
                    P("link-local", RosPropertyClassification.ObservationTyped),
                    P("slave", RosPropertyClassification.ObservationTyped))),
            Def(RosReadCommandId.InterfaceLists, "/interface/list/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                Props("interface_lists",
                    P(".id", RosPropertyClassification.RawOnly),
                    P("name"),
                    P("include"),
                    P("exclude"),
                    P("comment", redaction: RosRedactionPolicy.LogRedacted),
                    P("dynamic", RosPropertyClassification.ObservationTyped))),
            Def(RosReadCommandId.InterfaceListMembers, "/interface/list/member/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                Props("interface_list_members",
                    P(".id", RosPropertyClassification.RawOnly),
                    P("list"),
                    P("interface"),
                    P("disabled"),
                    P("comment", redaction: RosRedactionPolicy.LogRedacted),
                    P("dynamic", RosPropertyClassification.ObservationTyped))),
            Def(RosReadCommandId.Ipv4Filter, "/ip/firewall/filter/print", RosResultShape.OrderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                FirewallFilterProfiles.Ipv4Filter,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.Ipv6Filter, "/ipv6/firewall/filter/print", RosResultShape.OrderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                FirewallFilterProfiles.Ipv6Filter,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.Ipv4AddressLists, "/ip/firewall/address-list/print", RosResultShape.DigestedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                FirewallFilterProfiles.Ipv4AddressList),
            Def(RosReadCommandId.Ipv6AddressLists, "/ipv6/firewall/address-list/print", RosResultShape.DigestedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                FirewallFilterProfiles.Ipv6AddressList),
            Def(RosReadCommandId.Ipv4Nat, "/ip/firewall/nat/print", RosResultShape.OrderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv4Nat,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.Ipv6Nat, "/ipv6/firewall/nat/print", RosResultShape.OrderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv6Nat,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.Ipv4Raw, "/ip/firewall/raw/print", RosResultShape.OrderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv4Raw,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.Ipv6Raw, "/ipv6/firewall/raw/print", RosResultShape.OrderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv6Raw,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.Ipv4Mangle, "/ip/firewall/mangle/print", RosResultShape.OrderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv4Mangle,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.Ipv6Mangle, "/ipv6/firewall/mangle/print", RosResultShape.OrderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv6Mangle,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.RoutingTables, "/routing/table/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.RoutingTables),
            Def(RosReadCommandId.RoutingSettings, "/routing/settings/print", RosResultShape.Singleton, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingAssuranceAllowlistProfiles.RoutingSettings),
            Def(RosReadCommandId.RoutingRules, "/routing/rule/print", RosResultShape.OrderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.RoutingRules),
            Def(RosReadCommandId.Ipv4StaticRoutes, "/ip/route/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv4StaticRoutes,
                RosQueryProfile.StaticRoutes),
            Def(RosReadCommandId.Ipv6StaticRoutes, "/ipv6/route/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv6StaticRoutes,
                RosQueryProfile.StaticRoutes),
            Def(RosReadCommandId.Ipv4DefaultRouteState, "/ip/route/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.Pass1Only,
                RoutingDependencyProfiles.Ipv4DefaultRouteState,
                RosQueryProfile.Ipv4DefaultRoutes),
            Def(RosReadCommandId.Ipv6DefaultRouteState, "/ipv6/route/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.Pass1Only,
                RoutingDependencyProfiles.Ipv6DefaultRouteState,
                RosQueryProfile.Ipv6DefaultRoutes),
            Def(RosReadCommandId.RoutingFilterRules, "/routing/filter/rule/print", RosResultShape.OrderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                RoutingAssuranceAllowlistProfiles.RoutingFilterRules,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.RoutingFilterSelectRules, "/routing/filter/select-rule/print", RosResultShape.OrderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                RoutingAssuranceAllowlistProfiles.RoutingFilterSelectRules,
                RosQueryProfile.AllRows),
            Def(RosReadCommandId.Ipv4Settings, "/ip/settings/print", RosResultShape.Singleton, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv4Settings),
            Def(RosReadCommandId.Ipv6Settings, "/ipv6/settings/print", RosResultShape.Singleton, RosRequirement.Required, RosPassPolicy.BothPasses,
                RoutingDependencyProfiles.Ipv6Settings),
            Def(RosReadCommandId.VrrpInterfaces, "/interface/vrrp/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                VrrpDiscoveryProfiles.VrrpInterfaces),
            Def(RosReadCommandId.Bridges, "/interface/bridge/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                BridgeSwitchDiscoveryProfiles.Bridges),
            Def(RosReadCommandId.BridgePorts, "/interface/bridge/port/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                BridgeSwitchDiscoveryProfiles.BridgePorts),
            Def(RosReadCommandId.BridgeSettings, "/interface/bridge/settings/print", RosResultShape.Singleton, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                BridgeSwitchDiscoveryProfiles.BridgeSettings),
            Def(RosReadCommandId.BridgeVlans, "/interface/bridge/vlan/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                BridgeSwitchDiscoveryProfiles.BridgeVlans),
            Def(RosReadCommandId.EthernetSwitches, "/interface/ethernet/switch/print", RosResultShape.UnorderedCollection, RosRequirement.Optional, RosPassPolicy.BothPasses,
                BridgeSwitchDiscoveryProfiles.EthernetSwitches),
            Def(RosReadCommandId.EthernetSwitchPorts, "/interface/ethernet/switch/port/print", RosResultShape.UnorderedCollection, RosRequirement.Optional, RosPassPolicy.BothPasses,
                BridgeSwitchDiscoveryProfiles.EthernetSwitchPorts),
            Def(RosReadCommandId.Containers, "/container/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                PacketPathAllowlistProfiles.Containers),
            Def(RosReadCommandId.Apps, "/app/print", RosResultShape.UnorderedCollection, RosRequirement.Optional, RosPassPolicy.BothPasses,
                PacketPathAllowlistProfiles.Apps),
            Def(RosReadCommandId.VethInterfaces, "/interface/veth/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                PacketPathAllowlistProfiles.VethInterfaces),
            Def(RosReadCommandId.IpVrfs, "/ip/vrf/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                PacketPathAllowlistProfiles.IpVrfs),
            Def(RosReadCommandId.VlanInterfaces, "/interface/vlan/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                PacketPathAllowlistProfiles.VlanInterfaces),
            Def(RosReadCommandId.Ipv4Arp, "/ip/arp/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                EndpointAttributionAllowlistProfiles.Ipv4Arp),
            Def(RosReadCommandId.Ipv6Neighbors, "/ipv6/neighbor/print", RosResultShape.UnorderedCollection, RosRequirement.Required, RosPassPolicy.BothPasses,
                EndpointAttributionAllowlistProfiles.Ipv6Neighbors),
            Def(RosReadCommandId.DhcpServerLeases, "/ip/dhcp-server/lease/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                EndpointAttributionAllowlistProfiles.DhcpServerLeases),
            Def(RosReadCommandId.BridgeHosts, "/interface/bridge/host/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                EndpointAttributionAllowlistProfiles.BridgeHosts),
            Def(RosReadCommandId.DhcpSnoopingBindings, "/interface/bridge/dhcp-snooping/binding/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                EndpointAttributionAllowlistProfiles.DhcpSnoopingBindings),
            Def(RosReadCommandId.WireGuardPeers, "/interface/wireguard/peers/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                EndpointAttributionAllowlistProfiles.WireGuardPeers),
            Def(RosReadCommandId.IpsecActivePeers, "/ip/ipsec/active-peers/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                EndpointAttributionAllowlistProfiles.IpsecActivePeers),
            Def(RosReadCommandId.PppActiveSessions, "/ppp/active/print", RosResultShape.UnorderedCollection, RosRequirement.Conditional, RosPassPolicy.BothPasses,
                EndpointAttributionAllowlistProfiles.PppActiveSessions),
        ];

        Dictionary<RosReadCommandId, RosReadCommandDefinition> map = new(all.Length);
        foreach (RosReadCommandDefinition definition in all)
        {
            if (!map.TryAdd(definition.Id, definition))
            {
                throw new InvalidOperationException($"Duplicate command id '{definition.Id}'.");
            }

            ValidateNoForbiddenProperties(definition);
            ValidateReadOnlyPath(definition);
        }

        if (map.Count != Enum.GetValues<RosReadCommandId>().Length)
        {
            throw new InvalidOperationException("RosReadCommandRegistry is missing one or more command ids.");
        }

        ById = map;
        All = all;
    }

    public static IReadOnlyList<RosReadCommandDefinition> All { get; }

    public static RosReadCommandDefinition Get(RosReadCommandId id)
        => ById[id];

    public static bool TryGet(RosReadCommandId id, out RosReadCommandDefinition definition)
        => ById.TryGetValue(id, out definition!);

    /// <summary>True when the path appears in the allowlist (exact match).</summary>
    public static bool IsAllowlistedPath(string path)
        => All.Any(d => string.Equals(d.FixedPath, path, StringComparison.Ordinal));

    private static RosReadCommandDefinition Def(
        RosReadCommandId id,
        string path,
        RosResultShape shape,
        RosRequirement requirement,
        RosPassPolicy passPolicy,
        RosPropertyProfile profile,
        RosQueryProfile? query = null)
        => new(id, path, shape, requirement, passPolicy, profile, query ?? RosQueryProfile.None);

    private static RosPropertyProfile Props(string id, params RosPropertyDefinition[] properties)
        => new(id, properties);

    private static RosPropertyDefinition P(
        string name,
        RosPropertyClassification classification = RosPropertyClassification.ConfigTyped,
        RosRedactionPolicy redaction = RosRedactionPolicy.None)
    {
        if (string.Equals(name, "comment", StringComparison.Ordinal)
            || string.Equals(name, "note", StringComparison.Ordinal))
        {
            redaction = RosRedactionPolicy.LogRedacted;
        }

        return new RosPropertyDefinition(name, classification, redaction);
    }

    private static void ValidateNoForbiddenProperties(RosReadCommandDefinition definition)
    {
        foreach (RosPropertyDefinition property in definition.PropertyProfile.Properties)
        {
            if (SensitiveFieldRegistry.IsForbidden(property.RouterOsName))
            {
                throw new InvalidOperationException(
                    $"Command '{definition.Id}' requests forbidden property '{property.RouterOsName}'.");
            }
        }
    }

    private static void ValidateReadOnlyPath(RosReadCommandDefinition definition)
    {
        string path = definition.FixedPath;
        // Match whole path segments only — "/ip/address/print" must not trip on substring "add".
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0
            || !string.Equals(segments[^1], "print", StringComparison.Ordinal)
            || segments.Any(ForbiddenPathSegments.Contains))
        {
            throw new InvalidOperationException(
                $"Command '{definition.Id}' path '{path}' is not a read-only /print operation.");
        }
    }
}
