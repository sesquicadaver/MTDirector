using Mfc.RouterOs.Capabilities;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Complete discovery material from one stable-read attempt (M1-19…M1-22).</summary>
public sealed class RouterOsDiscoveryDataset
{
    public required SystemServiceDiscoveryResult System { get; init; }

    public required InterfaceAddressDiscoveryResult Interfaces { get; init; }

    public required FirewallFilterDiscoveryResult Firewall { get; init; }

    public required RoutingDependencyDiscoveryResult Routing { get; init; }

    public required VrrpDiscoveryResult Vrrp { get; init; }

    public required BridgeSwitchDiscoveryResult BridgeSwitch { get; init; }

    public PacketPathTopologyResult? PacketPathTopology { get; init; }

    public required CapabilityEvaluationResult Capabilities { get; init; }

    public required IReadOnlyDictionary<RosReadCommandId, RosReadCommandResult> CommandResults { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset CompletedAtUtc { get; init; }
}
