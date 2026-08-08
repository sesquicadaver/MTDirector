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
}
