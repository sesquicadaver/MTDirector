namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted routing context for one endpoint presence (M7.2-02).</summary>
public sealed class EndpointRoutingContextEntity
{
    public Guid PresenceId { get; set; }

    public Guid EndpointId { get; set; }

    public Guid SiteId { get; set; }

    public Guid NodeId { get; set; }

    public string? VlanId { get; set; }

    public string? Vrf { get; set; }

    public string SourceAddress { get; set; } = string.Empty;

    public string? CorporateRouteTraceJson { get; set; }

    public string? InternetRouteTraceJson { get; set; }

    public string? WazuhRouteTraceJson { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset? ValidUntil { get; set; }
}
