using Mfc.Domain.Endpoint;
using Mfc.Domain.Routing;

namespace Mfc.Application.Models;

/// <summary>Application view of one endpoint presence interval (M7.2-02).</summary>
public sealed class EndpointPresenceIntervalView
{
    public required Guid PresenceId { get; init; }

    public required Guid EndpointId { get; init; }

    public required Guid SiteId { get; init; }

    public required Guid NodeId { get; init; }

    public Guid? DeviceId { get; init; }

    public string? VlanId { get; init; }

    public string? Vrf { get; init; }

    public required string SourceAddress { get; init; }

    public string? MacAddress { get; init; }

    public required string AttributionCertainty { get; init; }

    public required DateTimeOffset ValidFromUtc { get; init; }

    public DateTimeOffset? ValidUntilUtc { get; init; }

    public static EndpointPresenceIntervalView FromDomain(EndpointPresenceInterval interval)
    {
        ArgumentNullException.ThrowIfNull(interval);
        return new EndpointPresenceIntervalView
        {
            PresenceId = interval.PresenceId.Value,
            EndpointId = interval.EndpointId.Value,
            SiteId = interval.SiteId.Value,
            NodeId = interval.NodeId.Value,
            DeviceId = interval.DeviceId?.Value,
            VlanId = interval.VlanId,
            Vrf = interval.Vrf,
            SourceAddress = interval.SourceAddress,
            MacAddress = interval.MacAddress,
            AttributionCertainty = interval.AttributionCertainty.ToString(),
            ValidFromUtc = interval.ValidFrom,
            ValidUntilUtc = interval.ValidUntil,
        };
    }
}

/// <summary>Application view of endpoint routing context with trace summaries (M7.2-02).</summary>
public sealed class EndpointRoutingContextView
{
    public required Guid EndpointId { get; init; }

    public required Guid PresenceId { get; init; }

    public required Guid SiteId { get; init; }

    public required Guid NodeId { get; init; }

    public string? VlanId { get; init; }

    public string? Vrf { get; init; }

    public required string SourceAddress { get; init; }

    public RouteResolutionTraceSummaryView? CorporateRouteTrace { get; init; }

    public RouteResolutionTraceSummaryView? InternetRouteTrace { get; init; }

    public RouteResolutionTraceSummaryView? WazuhRouteTrace { get; init; }

    public required DateTimeOffset ValidFromUtc { get; init; }

    public DateTimeOffset? ValidUntilUtc { get; init; }

    public static EndpointRoutingContextView FromDomain(EndpointRoutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new EndpointRoutingContextView
        {
            EndpointId = context.EndpointId.Value,
            PresenceId = context.PresenceId.Value,
            SiteId = context.SiteId.Value,
            NodeId = context.NodeId.Value,
            VlanId = context.VlanId,
            Vrf = context.Vrf,
            SourceAddress = context.SourceAddress,
            CorporateRouteTrace = RouteResolutionTraceSummaryView.FromTrace(context.CorporateRouteTrace),
            InternetRouteTrace = RouteResolutionTraceSummaryView.FromTrace(context.InternetRouteTrace),
            WazuhRouteTrace = RouteResolutionTraceSummaryView.FromTrace(context.WazuhRouteTrace),
            ValidFromUtc = context.ValidFrom,
            ValidUntilUtc = context.ValidUntil,
        };
    }
}
