using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Routing;

/// <summary>
/// Latency profile bound to a routing result, not destination alone (M7.1 Spec §13).
/// Optional routing_table / vrf / source_interface on the profile are trace query hints only;
/// bound probes take table, VRF, and egress interface from <see cref="RouteResolutionTrace"/>.
/// </summary>
public sealed class NetworkPathProfile
{
    public required DeviceId SourceDevice { get; init; }

    public string? SourceAddress { get; init; }

    public string? SourceInterface { get; init; }

    public string? RoutingTable { get; init; }

    public string? Vrf { get; init; }

    public required string Destination { get; init; }

    public string? ExpectedRoutePrefix { get; init; }

    public IReadOnlyList<string> ExpectedNextHops { get; init; } = [];

    public IReadOnlyList<string> ExpectedEgressInterfaces { get; init; } = [];

    public string? ExpectedExecutionPath { get; init; }

    public double? MaxLoss { get; init; }

    public double? MaxRtt { get; init; }

    public double? MaxJitter { get; init; }

    /// <summary>Maximum fractional RTT increase vs baseline (e.g. 0.25 = 25%).</summary>
    public double? MaxRegression { get; init; }

    public bool Critical { get; init; }

    public int ProbeTimeoutMilliseconds { get; init; } = 1000;
}

/// <summary>Profile plus trace-derived probe parameters (M7.1-08).</summary>
public sealed class NetworkPathProbeBinding
{
    public required NetworkPathProfile Profile { get; init; }

    public required RoutingBoundLatencyProbe Probe { get; init; }

    public required RoutePathFingerprint PathFingerprint { get; init; }
}
