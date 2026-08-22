namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted endpoint presence interval (M7.2-02).</summary>
public sealed class EndpointPresenceIntervalEntity
{
    public Guid PresenceId { get; set; }

    public Guid EndpointId { get; set; }

    public Guid SiteId { get; set; }

    public Guid NodeId { get; set; }

    public Guid? DeviceId { get; set; }

    public string? VlanId { get; set; }

    public string? Vrf { get; set; }

    public string SourceAddress { get; set; } = string.Empty;

    public string? MacAddress { get; set; }

    public int AttributionCertainty { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset? ValidUntil { get; set; }
}
