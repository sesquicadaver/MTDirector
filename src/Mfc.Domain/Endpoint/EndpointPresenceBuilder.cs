using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.Domain.Endpoint;

/// <summary>Builds endpoint presence and routing context from attribution output (M7.2-02).</summary>
public static class EndpointPresenceBuilder
{
    /// <summary>Builds a new active presence interval from attribution resolver output.</summary>
    public static EndpointPresenceInterval BuildInterval(
        EndpointId endpointId,
        PresenceId presenceId,
        EndpointAttributionResult attribution,
        EndpointAttributionQuery query,
        DateTimeOffset validFrom,
        string? vrf = null)
    {
        ArgumentNullException.ThrowIfNull(attribution);
        ArgumentNullException.ThrowIfNull(query);

        (SiteId siteId, NodeId nodeId, DeviceId? deviceId) = ResolveInventoryAnchors(attribution, query);
        string? vlanId = FindHopValue(attribution, EndpointAttributionHopKind.Vlan);
        string? mac = FindHopValue(attribution, EndpointAttributionHopKind.Mac);

        return EndpointPresenceInterval.Create(
            presenceId,
            endpointId,
            siteId,
            nodeId,
            query.IpAddress.Trim(),
            attribution.Certainty,
            validFrom,
            deviceId,
            vlanId,
            NormalizeOptional(vrf),
            mac);
    }

    private static (SiteId SiteId, NodeId NodeId, DeviceId? DeviceId) ResolveInventoryAnchors(
        EndpointAttributionResult attribution,
        EndpointAttributionQuery query)
    {
        SiteId? site = query.SiteId ?? TryParseSite(attribution);
        NodeId? node = query.NodeId ?? TryParseNode(attribution);
        DeviceId? device = query.DeviceId ?? TryParseDevice(attribution);

        if (site is null)
        {
            throw new DomainInvariantException($"Site_id is required ({EndpointPresenceCodes.MissingSiteId}).");
        }

        if (node is null)
        {
            throw new DomainInvariantException($"Node_id is required ({EndpointPresenceCodes.MissingNodeId}).");
        }

        return (site.Value, node.Value, device);
    }

    private static SiteId? TryParseSite(EndpointAttributionResult attribution)
    {
        string? value = FindHopValue(attribution, EndpointAttributionHopKind.Site);
        return Guid.TryParse(value, out Guid id) ? new SiteId(id) : null;
    }

    private static NodeId? TryParseNode(EndpointAttributionResult attribution)
    {
        string? value = FindHopValue(attribution, EndpointAttributionHopKind.Node);
        return Guid.TryParse(value, out Guid id) ? new NodeId(id) : null;
    }

    private static DeviceId? TryParseDevice(EndpointAttributionResult attribution)
    {
        string? value = FindHopValue(attribution, EndpointAttributionHopKind.Device);
        return Guid.TryParse(value, out Guid id) ? new DeviceId(id) : null;
    }

    private static string? FindHopValue(EndpointAttributionResult attribution, EndpointAttributionHopKind kind)
        => attribution.Chain.Hops.FirstOrDefault(h => h.Kind == kind)?.Value;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Builds <see cref="EndpointRoutingContext"/> with optional route traces (M7.2-02).</summary>
public static class EndpointRoutingContextBuilder
{
    public static EndpointRoutingContext Build(
        EndpointPresenceInterval interval,
        RouteResolutionTrace? corporateRouteTrace = null,
        RouteResolutionTrace? internetRouteTrace = null,
        RouteResolutionTrace? wazuhRouteTrace = null)
    {
        ArgumentNullException.ThrowIfNull(interval);
        return EndpointRoutingContext.Create(
            interval,
            corporateRouteTrace,
            internetRouteTrace,
            wazuhRouteTrace);
    }

    /// <summary>
    /// Builds routing context directly from attribution output and optional trace triple.
    /// </summary>
    public static EndpointRoutingContext Build(
        EndpointId endpointId,
        PresenceId presenceId,
        EndpointAttributionResult attribution,
        EndpointAttributionQuery query,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        RouteResolutionTrace? corporateRouteTrace = null,
        RouteResolutionTrace? internetRouteTrace = null,
        RouteResolutionTrace? wazuhRouteTrace = null,
        string? vrf = null)
    {
        EndpointPresenceInterval interval = EndpointPresenceBuilder.BuildInterval(
            endpointId,
            presenceId,
            attribution,
            query,
            validFrom,
            vrf);
        EndpointPresenceInterval reconstituted = EndpointPresenceInterval.Reconstitute(
            interval.PresenceId,
            interval.EndpointId,
            interval.SiteId,
            interval.NodeId,
            interval.SourceAddress,
            interval.AttributionCertainty,
            interval.ValidFrom,
            validUntil,
            interval.DeviceId,
            interval.VlanId,
            interval.Vrf,
            interval.MacAddress);
        return EndpointRoutingContext.Create(
            reconstituted,
            corporateRouteTrace,
            internetRouteTrace,
            wazuhRouteTrace);
    }
}
