using Mfc.RouterOs.Capabilities;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Reads the full allowlisted discovery dataset from one authenticated session.</summary>
public static class RouterOsDiscoveryReader
{
    public static async Task<RouterOsDiscoveryDataset> ReadAsync(
        RosSession session,
        StableReadExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(context);
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

        List<Func<CancellationToken, Task<(RosReadCommandId Id, RosReadCommandResult Result)>>> actions = [];
        foreach (RosReadCommandId commandId in RouterOsDiscoveryCommandCatalog.All)
        {
            RosReadCommandId captured = commandId;
            actions.Add(async ct =>
            {
                RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
                    session,
                    captured,
                    context.CommandTimeout,
                    ct).ConfigureAwait(false);
                return (captured, result);
            });
        }

        IReadOnlyList<(RosReadCommandId Id, RosReadCommandResult Result)> executed =
            await context.Parallelism.RunAllAsync(actions, cancellationToken).ConfigureAwait(false);

        Dictionary<RosReadCommandId, RosReadCommandResult> commandResults = new();
        foreach ((RosReadCommandId id, RosReadCommandResult result) in executed)
        {
            commandResults[id] = result;
        }

        SystemServiceDiscoveryResult system = SystemServiceDiscovery.BuildResult(
            commandResults[RosReadCommandId.SystemIdentity],
            commandResults[RosReadCommandId.SystemResource],
            commandResults[RosReadCommandId.SystemPackages],
            commandResults[RosReadCommandId.SystemClock],
            commandResults[RosReadCommandId.IpServices],
            commandResults[RosReadCommandId.SystemRouterboard]);

        InterfaceAddressDiscoveryResult interfaces = InterfaceAddressDiscovery.BuildResult(
            commandResults[RosReadCommandId.Interfaces],
            commandResults[RosReadCommandId.Ipv4Addresses],
            commandResults[RosReadCommandId.Ipv6Addresses],
            commandResults[RosReadCommandId.InterfaceLists],
            commandResults[RosReadCommandId.InterfaceListMembers]);

        FirewallFilterDiscoveryResult firewall = FirewallFilterDiscovery.BuildResult(
            commandResults[RosReadCommandId.Ipv4Filter],
            commandResults[RosReadCommandId.Ipv6Filter],
            commandResults[RosReadCommandId.Ipv4AddressLists],
            commandResults[RosReadCommandId.Ipv6AddressLists]);

        RoutingDependencyDiscoveryResult routing = RoutingDependencyDiscovery.BuildResult(
            commandResults[RosReadCommandId.RoutingTables],
            commandResults[RosReadCommandId.RoutingSettings],
            commandResults[RosReadCommandId.RoutingRules],
            commandResults[RosReadCommandId.IpVrfs],
            commandResults[RosReadCommandId.Ipv4StaticRoutes],
            commandResults[RosReadCommandId.Ipv6StaticRoutes],
            commandResults[RosReadCommandId.Ipv4DefaultRouteState],
            commandResults[RosReadCommandId.Ipv6DefaultRouteState],
            commandResults[RosReadCommandId.RoutingFilterRules],
            commandResults[RosReadCommandId.RoutingFilterSelectRules],
            commandResults[RosReadCommandId.Ipv4Nat],
            commandResults[RosReadCommandId.Ipv6Nat],
            commandResults[RosReadCommandId.Ipv4Raw],
            commandResults[RosReadCommandId.Ipv6Raw],
            commandResults[RosReadCommandId.Ipv4Mangle],
            commandResults[RosReadCommandId.Ipv6Mangle],
            commandResults[RosReadCommandId.Ipv4Settings],
            commandResults[RosReadCommandId.Ipv6Settings]);

        VrrpDiscoveryResult vrrp = VrrpDiscovery.BuildResult(
            commandResults[RosReadCommandId.VrrpInterfaces],
            interfaces);

        BridgeSwitchDiscoveryResult bridgeSwitch = BridgeSwitchDiscovery.BuildResult(
            commandResults[RosReadCommandId.Bridges],
            commandResults[RosReadCommandId.BridgePorts],
            commandResults[RosReadCommandId.BridgeSettings],
            commandResults[RosReadCommandId.BridgeVlans],
            commandResults[RosReadCommandId.EthernetSwitches],
            commandResults[RosReadCommandId.EthernetSwitchPorts]);

        PacketPathTopologyResult packetPath = PacketPathTopologyDiscovery.BuildResult(
            commandResults[RosReadCommandId.Containers],
            commandResults[RosReadCommandId.Apps],
            commandResults[RosReadCommandId.VethInterfaces],
            commandResults[RosReadCommandId.VlanInterfaces],
            bridgeSwitch,
            commandResults[RosReadCommandId.IpVrfs]);

        CapabilityEvaluationResult capabilities = CapabilityProfileEvaluator.Evaluate(system);

        return new RouterOsDiscoveryDataset
        {
            System = system,
            Interfaces = interfaces,
            Firewall = firewall,
            Routing = routing,
            Vrrp = vrrp,
            BridgeSwitch = bridgeSwitch,
            PacketPathTopology = packetPath,
            Capabilities = capabilities,
            CommandResults = commandResults,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
