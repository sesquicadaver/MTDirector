using Mfc.Domain.Deployment;

namespace Mfc.Domain.Routing;

/// <summary>
/// Latency probe parameters derived from a routing trace (M7.1-08).
/// Table, VRF, and egress interface come from the trace; destination from the profile.
/// </summary>
public sealed class RoutingBoundLatencyProbe
{
    public required string Destination { get; init; }

    public string? SourceAddress { get; init; }

    public string? RoutingTable { get; init; }

    public string? SelectedVrf { get; init; }

    public string? Interface { get; init; }

    public required RoutePathFingerprint PathFingerprint { get; init; }

    public int TimeoutMilliseconds { get; init; } = DeploymentProbe.MinTimeoutMs;

    /// <summary>Maps to bounded ROUTER_PING deployment probe (Spec §33).</summary>
    public DeploymentProbe ToDeploymentProbe()
    {
        int timeout = TimeoutMilliseconds is >= DeploymentProbe.MinTimeoutMs and <= DeploymentProbe.MaxTimeoutMs
            ? TimeoutMilliseconds
            : DeploymentProbe.MinTimeoutMs;
        return new DeploymentProbe(
            DeploymentProbeKind.RouterPing,
            Destination,
            timeout,
            sourceAddress: SourceAddress,
            routingTable: RoutingTable,
            @interface: Interface);
    }
}
