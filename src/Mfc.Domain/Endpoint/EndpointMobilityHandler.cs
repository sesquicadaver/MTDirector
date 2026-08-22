using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.Domain.Endpoint;

/// <summary>Corporate/internet/Wazuh probe destinations for mobility trace recompute (M7.2-03).</summary>
public sealed class EndpointMobilityProbeTargets
{
    public required string CorporateDestination { get; init; }

    public required string InternetDestination { get; init; }

    public required string WazuhDestination { get; init; }

    public string Family { get; init; } = "ipv4";
}

/// <summary>Outcome of endpoint mobility handling during presence migration (M7.2-03).</summary>
public sealed record EndpointMobilityOutcome(
    EndpointRoutingContext RoutingContext,
    NodeId EnforcementNodeId,
    ResponseAssessment? InvalidatedAssessment,
    bool AutoDeploySuppressed);

/// <summary>
/// Handles endpoint mobility: invalidate assessment, recompute traces, resolve enforcement node (M7.2-03 / M7.1 §15).
/// Does not trigger deployment.
/// </summary>
public static class EndpointMobilityHandler
{
    /// <summary>Returns true when the opened interval changes routing anchors relative to the prior active interval.</summary>
    public static bool IsMobilityEvent(
        EndpointPresenceInterval? priorActive,
        EndpointPresenceInterval opened)
    {
        ArgumentNullException.ThrowIfNull(opened);
        if (priorActive is null)
        {
            return false;
        }

        return !priorActive.SiteId.Equals(opened.SiteId)
               || !priorActive.NodeId.Equals(opened.NodeId)
               || !string.Equals(priorActive.VlanId, opened.VlanId, StringComparison.Ordinal)
               || !string.Equals(priorActive.Vrf, opened.Vrf, StringComparison.Ordinal)
               || !string.Equals(
                   priorActive.SourceAddress,
                   opened.SourceAddress,
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies mobility rules when an active incident assessment exists: invalidate, recompute traces, suppress deploy.
    /// </summary>
    public static EndpointMobilityOutcome ProcessActiveIncidentMobility(
        EndpointPresenceInterval opened,
        ResponseAssessment activeAssessment,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational,
        EndpointMobilityProbeTargets probeTargets,
        DateTimeOffset processedAt)
    {
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(activeAssessment);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operational);
        ArgumentNullException.ThrowIfNull(probeTargets);

        if (!activeAssessment.IsActive)
        {
            throw new DomainInvariantException("Active incident mobility requires an active response assessment.");
        }

        if (!activeAssessment.EndpointId.Equals(opened.EndpointId))
        {
            throw new DomainInvariantException("Response assessment endpoint_id mismatch.");
        }

        ResponseAssessment invalidated = activeAssessment.Invalidate(
            processedAt,
            EndpointMobilityCodes.MobilityInvalidation);
        (RouteResolutionTrace corporate, RouteResolutionTrace internet, RouteResolutionTrace wazuh) =
            RecomputeTraceTriple(opened, configuration, operational, probeTargets);
        EndpointRoutingContext routingContext = EndpointRoutingContextBuilder.Build(
            opened,
            corporate,
            internet,
            wazuh);
        NodeId enforcementNode = ResolveEnforcementNode(opened);
        return new EndpointMobilityOutcome(
            routingContext,
            enforcementNode,
            invalidated,
            AutoDeploySuppressed: true);
    }

    /// <summary>Resolves the node that controls routing for the current endpoint presence (M7.1 §15).</summary>
    public static NodeId ResolveEnforcementNode(EndpointPresenceInterval opened)
    {
        ArgumentNullException.ThrowIfNull(opened);
        return opened.NodeId;
    }

    private static (RouteResolutionTrace Corporate, RouteResolutionTrace Internet, RouteResolutionTrace Wazuh)
        RecomputeTraceTriple(
            EndpointPresenceInterval opened,
            RoutingConfigurationSnapshot configuration,
            RoutingOperationalSnapshot operational,
            EndpointMobilityProbeTargets probeTargets)
    {
        RouteResolutionTrace corporate = RouteResolutionTraceEngine.Analyze(
            BuildProbe(opened, probeTargets, probeTargets.CorporateDestination),
            configuration,
            operational);
        RouteResolutionTrace internet = RouteResolutionTraceEngine.Analyze(
            BuildProbe(opened, probeTargets, probeTargets.InternetDestination),
            configuration,
            operational);
        RouteResolutionTrace wazuh = RouteResolutionTraceEngine.Analyze(
            BuildProbe(opened, probeTargets, probeTargets.WazuhDestination),
            configuration,
            operational);
        return (corporate, internet, wazuh);
    }

    private static RouteResolutionQuery BuildProbe(
        EndpointPresenceInterval opened,
        EndpointMobilityProbeTargets probeTargets,
        string destination)
        => new()
        {
            Family = probeTargets.Family,
            SourceAddress = opened.SourceAddress,
            DestinationAddress = destination,
            InitialVrf = opened.Vrf,
        };
}
