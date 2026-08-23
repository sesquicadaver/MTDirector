namespace Mfc.RouterOs.Commands;

/// <summary>Compile-time allowlisted read-command identifiers (Read Adapter Spec §17).</summary>
public enum RosReadCommandId
{
    SystemIdentity = 1,
    SystemResource = 2,
    SystemRouterboard = 3,
    SystemPackages = 4,
    IpServices = 5,
    Interfaces = 6,
    Ipv4Addresses = 7,
    Ipv6Addresses = 8,
    InterfaceLists = 9,
    InterfaceListMembers = 10,
    Ipv4Filter = 11,
    Ipv6Filter = 12,
    Ipv4AddressLists = 13,
    Ipv6AddressLists = 14,
    Ipv4Nat = 15,
    Ipv6Nat = 16,
    Ipv4Raw = 17,
    Ipv6Raw = 18,
    Ipv4Mangle = 19,
    Ipv6Mangle = 20,
    RoutingTables = 21,
    RoutingRules = 22,
    Ipv4StaticRoutes = 23,
    Ipv6StaticRoutes = 24,
    Ipv4DefaultRouteState = 25,
    Ipv6DefaultRouteState = 26,
    Ipv4Settings = 27,
    Ipv6Settings = 28,
    VrrpInterfaces = 29,
    Bridges = 30,
    BridgePorts = 31,
    BridgeSettings = 32,
    BridgeVlans = 33,
    EthernetSwitches = 34,
    EthernetSwitchPorts = 35,

    /// <summary>Added for M1-11 system discovery (not in original §17 table).</summary>
    SystemClock = 36,

    /// <summary>Packet-path / container weave (N1-01, next-1).</summary>
    Containers = 37,
    Apps = 38,
    VethInterfaces = 39,
    IpVrfs = 40,

    /// <summary>L3 VLAN interfaces for topology projection (N1-02, next-1).</summary>
    VlanInterfaces = 41,

    /// <summary>Routing assurance reads (M7.1-01 / Spec §3).</summary>
    RoutingSettings = 42,
    RoutingFilterRules = 43,
    RoutingFilterSelectRules = 44,

    /// <summary>Endpoint attribution reads (M7.2-01 / next-2 §3).</summary>
    Ipv4Arp = 45,
    Ipv6Neighbors = 46,
    DhcpServerLeases = 47,
    BridgeHosts = 48,
    DhcpSnoopingBindings = 49,
    WireGuardPeers = 50,
    IpsecActivePeers = 51,
    PppActiveSessions = 52,

    /// <summary>On-demand connection-tracking reads (M7.3-03 / next-2 §2).</summary>
    Ipv4FirewallConnections = 53,
    Ipv6FirewallConnections = 54,
}
