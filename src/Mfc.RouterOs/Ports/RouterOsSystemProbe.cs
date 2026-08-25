using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Capabilities;
using Mfc.RouterOs.Capabilities;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Ports;

/// <summary>Allowlisted system discovery + capability evaluation for identity probes (P2-04).</summary>
public static class RouterOsSystemProbe
{
    public static async Task<RouterOsProbeResult> ProbeAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        SystemServiceDiscoveryResult discovery = await SystemServiceDiscovery
            .DiscoverAsync(session, cancellationToken)
            .ConfigureAwait(false);

        CapabilityEvaluationResult evaluation = CapabilityProfileEvaluator.Evaluate(discovery);
        string identity = discovery.Identity.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(identity))
        {
            identity = "unknown";
        }

        return new RouterOsProbeResult
        {
            Identity = identity,
            SupportState = evaluation.Profile.SupportState,
        };
    }
}
