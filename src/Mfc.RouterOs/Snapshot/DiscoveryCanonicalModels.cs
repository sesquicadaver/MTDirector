using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Snapshots;
using Mfc.Domain.Topology;
using Mfc.RouterOs.Discovery;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Optional discovery inputs for menu-specific canonical projection (M1-22 / N1-05).</summary>
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

    /// <summary>
    /// Optional packet-path topology used only to emit minimal membership sections (N1-05).
    /// Full graph is not projected.
    /// </summary>
    /// <remarks>
    /// Capture assemblers that call <see cref="DiscoveryCanonicalProjector"/> must populate this
    /// from packet-path topology discovery when marker-ready zone resolve is required.
    /// Until a live snapshot capture port uses the projector (same M1-22 seam), production captures
    /// may omit these sections; Domain/App still resolve plain IF names and typed-miss markers
    /// without poisoning.
    /// </remarks>
    public PacketPathTopologyResult? PacketPathTopology { get; init; }

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
