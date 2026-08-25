using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Union of allowlisted read commands executed during production snapshot capture.</summary>
public static class RouterOsDiscoveryCommandCatalog
{
    private static readonly RosReadCommandId[] SystemCommands =
    [
        RosReadCommandId.SystemIdentity,
        RosReadCommandId.SystemResource,
        RosReadCommandId.SystemRouterboard,
        RosReadCommandId.SystemPackages,
        RosReadCommandId.SystemClock,
        RosReadCommandId.IpServices,
    ];

    private static readonly RosReadCommandId[] InterfaceCommands =
    [
        RosReadCommandId.Interfaces,
        RosReadCommandId.Ipv4Addresses,
        RosReadCommandId.Ipv6Addresses,
        RosReadCommandId.InterfaceLists,
        RosReadCommandId.InterfaceListMembers,
    ];

    private static readonly RosReadCommandId[] FirewallCommands =
    [
        RosReadCommandId.Ipv4Filter,
        RosReadCommandId.Ipv6Filter,
        RosReadCommandId.Ipv4AddressLists,
        RosReadCommandId.Ipv6AddressLists,
    ];

    private static readonly RosReadCommandId[] VrrpCommands = [RosReadCommandId.VrrpInterfaces];

    private static readonly RosReadCommandId[] AllCaptureCommands = BuildCatalog();

    /// <summary>All read commands fetched for one discovery pass (deduplicated, stable order).</summary>
    public static IReadOnlyList<RosReadCommandId> All { get; } = AllCaptureCommands;

    private static RosReadCommandId[] BuildCatalog()
    {
        HashSet<RosReadCommandId> set = [];
        foreach (RosReadCommandId id in SystemCommands)
        {
            set.Add(id);
        }

        foreach (RosReadCommandId id in InterfaceCommands)
        {
            set.Add(id);
        }

        foreach (RosReadCommandId id in FirewallCommands)
        {
            set.Add(id);
        }

        foreach (RosReadCommandId id in RoutingDependencyDiscovery.DiscoveryCommandIds)
        {
            set.Add(id);
        }

        foreach (RosReadCommandId id in VrrpCommands)
        {
            set.Add(id);
        }

        foreach (RosReadCommandId id in BridgeSwitchDiscovery.DiscoveryCommandIds)
        {
            set.Add(id);
        }

        foreach (RosReadCommandId id in PacketPathTopologyDiscovery.DiscoveryCommandIds)
        {
            set.Add(id);
        }

        return set.OrderBy(static id => (int)id).ToArray();
    }
}
