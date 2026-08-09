using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Snapshots;
using Mfc.Domain.Topology;
using Mfc.RouterOs.Discovery;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Optional discovery inputs for menu-specific canonical projection (M1-22).</summary>
public sealed class DiscoveryCanonicalInput
{
    public SystemServiceDiscoveryResult? System { get; init; }

    public InterfaceAddressDiscoveryResult? Interfaces { get; init; }

    public FirewallFilterDiscoveryResult? Firewall { get; init; }

    public RoutingDependencyDiscoveryResult? Routing { get; init; }

    public VrrpDiscoveryResult? Vrrp { get; init; }

    public BridgeSwitchDiscoveryResult? BridgeSwitch { get; init; }

    public CapabilityProfile? Capabilities { get; init; }

    public NodeTopologyValidationResult? TopologyValidation { get; init; }

    public int SchemaVersion { get; init; } = 1;
}

/// <summary>Fully projected canonical device snapshot with separated hashes.</summary>
public sealed class CanonicalDeviceSnapshot
{
    public required int SchemaVersion { get; init; }

    public required IReadOnlyList<CanonicalSection> ConfigurationSections { get; init; }

    public required IReadOnlyList<CanonicalSection> ObservationSections { get; init; }

    public required ConfigurationHash ConfigurationHash { get; init; }

    public required ObservationHash ObservationHash { get; init; }

    public required SnapshotHash SnapshotHash { get; init; }
}
