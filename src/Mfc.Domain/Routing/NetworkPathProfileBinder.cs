using System.Globalization;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Routing;

/// <summary>
/// Binds <see cref="NetworkPathProfile"/> latency probes to routing trace results (M7.1-08).
/// Probe table, VRF, and egress interface are taken from the trace, not the profile hints alone.
/// </summary>
public static class NetworkPathProfileBinder
{
    /// <summary>
    /// Creates a trace-bound latency probe. Destination comes from the profile;
    /// routing table, VRF, and egress interface from the trace.
    /// </summary>
    public static NetworkPathProbeBinding Bind(NetworkPathProfile profile, RouteResolutionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(trace);
        ValidateProfile(profile);
        ValidateTraceMatchesProfile(profile, trace);

        RoutePathFingerprint fingerprint = RoutePathFingerprint.FromTrace(trace);
        string destination = profile.Destination.Trim();
        string? sourceAddress = ResolveSourceAddress(profile, trace);
        string? routingTable = Normalize(trace.SelectedTable);
        string? selectedVrf = Normalize(trace.SelectedVrf);
        string? egressInterface = ResolveEgressInterface(trace);

        int timeout = profile.ProbeTimeoutMilliseconds;
        if (timeout is < Deployment.DeploymentProbe.MinTimeoutMs or > Deployment.DeploymentProbe.MaxTimeoutMs)
        {
            timeout = Deployment.DeploymentProbe.MinTimeoutMs;
        }

        RoutingBoundLatencyProbe probe = new()
        {
            Destination = destination,
            SourceAddress = sourceAddress,
            RoutingTable = routingTable,
            SelectedVrf = selectedVrf,
            Interface = egressInterface,
            PathFingerprint = fingerprint,
            TimeoutMilliseconds = timeout,
        };

        return new NetworkPathProbeBinding
        {
            Profile = profile,
            Probe = probe,
            PathFingerprint = fingerprint,
        };
    }

    /// <summary>Attaches bindings to traces matched by destination address.</summary>
    public static IReadOnlyList<RouteResolutionTrace> AttachBindings(
        IReadOnlyList<RouteResolutionTrace> traces,
        IReadOnlyList<NetworkPathProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(traces);
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.Count == 0)
        {
            return traces;
        }

        List<RouteResolutionTrace> enriched = new(traces.Count);
        foreach (RouteResolutionTrace trace in traces)
        {
            List<NetworkPathProbeBinding> bindings = [];
            foreach (NetworkPathProfile profile in profiles)
            {
                if (!TraceMatchesProfile(profile, trace))
                {
                    continue;
                }

                bindings.Add(Bind(profile, trace));
            }

            enriched.Add(bindings.Count == 0 ? trace : trace.WithNetworkPathProbeBindings(bindings));
        }

        return enriched;
    }

    private static void ValidateProfile(NetworkPathProfile profile)
    {
        if (profile.SourceDevice.Equals(default(DeviceId)))
        {
            throw new DomainInvariantException("NetworkPathProfile source_device is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.Destination))
        {
            throw new DomainInvariantException("NetworkPathProfile destination is required.");
        }

        if (!Deployment.DeploymentProbe.TryParseLiteralIp(profile.Destination.Trim(), out _))
        {
            throw new DomainInvariantException("NetworkPathProfile destination must be a literal IP address.");
        }
    }

    private static void ValidateTraceMatchesProfile(NetworkPathProfile profile, RouteResolutionTrace trace)
    {
        if (!TraceMatchesProfile(profile, trace))
        {
            throw new DomainInvariantException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Trace destination '{trace.DestinationAddress ?? "<none>"}' does not match profile destination '{profile.Destination}'."));
        }
    }

    private static bool TraceMatchesProfile(NetworkPathProfile profile, RouteResolutionTrace trace)
    {
        if (string.IsNullOrWhiteSpace(trace.DestinationAddress))
        {
            return false;
        }

        return string.Equals(
            profile.Destination.Trim(),
            trace.DestinationAddress.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveSourceAddress(NetworkPathProfile profile, RouteResolutionTrace trace)
        => Normalize(profile.SourceAddress)
           ?? Normalize(trace.PreferredSource)
           ?? Normalize(trace.SourceAddress);

    private static string? ResolveEgressInterface(RouteResolutionTrace trace)
    {
        foreach (string egress in trace.EgressInterfaces)
        {
            string? normalized = Normalize(egress);
            if (normalized is not null)
            {
                return normalized;
            }
        }

        return Normalize(trace.IngressInterface);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
