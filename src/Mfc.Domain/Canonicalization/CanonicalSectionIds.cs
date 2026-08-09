namespace Mfc.Domain.Canonicalization;

/// <summary>
/// Section registry identifiers (<c>mfc.section-registry/1</c>) used by menu-specific canonical snapshots (M1-22).
/// </summary>
public static class CanonicalSectionIds
{
    public const string RegistryVersion = "1";

    public const string SystemIdentity = "system.identity";
    public const string SystemResource = "system.resource";
    public const string ManagementIpServices = "management.ip-services";
    public const string NetworkInterfaces = "network.interfaces";
    public const string NetworkIpv4Addresses = "network.ipv4.addresses";
    public const string NetworkIpv6Addresses = "network.ipv6.addresses";
    public const string NetworkInterfaceLists = "network.interface-lists";
    public const string FirewallIpv4Filter = "firewall.ipv4.filter";
    public const string FirewallIpv6Filter = "firewall.ipv6.filter";
    public const string FirewallIpv4AddressLists = "firewall.ipv4.address-lists";
    public const string FirewallIpv6AddressLists = "firewall.ipv6.address-lists";
    public const string FirewallIpv4Nat = "firewall.ipv4.nat";
    public const string FirewallIpv6Nat = "firewall.ipv6.nat";
    public const string FirewallIpv4Raw = "firewall.ipv4.raw";
    public const string FirewallIpv6Raw = "firewall.ipv6.raw";
    public const string FirewallIpv4Mangle = "firewall.ipv4.mangle";
    public const string FirewallIpv6Mangle = "firewall.ipv6.mangle";
    public const string RoutingTables = "routing.tables";
    public const string RoutingRules = "routing.rules";
    public const string RoutingIpv4StaticRoutes = "routing.ipv4.static-routes";
    public const string RoutingIpv6StaticRoutes = "routing.ipv6.static-routes";
    public const string RoutingIpv4DefaultState = "routing.ipv4.default-state";
    public const string RoutingIpv6DefaultState = "routing.ipv6.default-state";
    public const string NetworkIpv4Settings = "network.ipv4.settings";
    public const string NetworkIpv6Settings = "network.ipv6.settings";
    public const string HaVrrp = "ha.vrrp";
    public const string BridgeInstances = "bridge.instances";
    public const string BridgePorts = "bridge.ports";
    public const string BridgeSettings = "bridge.settings";
    public const string BridgeVlans = "bridge.vlans";
    public const string SwitchInstances = "switch.instances";
    public const string SwitchPorts = "switch.ports";
    public const string CapabilitiesDevice = "capabilities.device";
    public const string CompatibilityFindings = "compatibility.findings";
    public const string CompatibilityUnknownProperties = "compatibility.unknown-properties";
    public const string TopologyValidation = "topology.validation";

    /// <summary>True when the section preserves RouterOS reply order (firewall/routing rules).</summary>
    public static bool IsOrdered(string sectionId)
        => sectionId is FirewallIpv4Filter
            or FirewallIpv6Filter
            or FirewallIpv4Nat
            or FirewallIpv6Nat
            or FirewallIpv4Raw
            or FirewallIpv6Raw
            or FirewallIpv4Mangle
            or FirewallIpv6Mangle
            or RoutingRules;
}
