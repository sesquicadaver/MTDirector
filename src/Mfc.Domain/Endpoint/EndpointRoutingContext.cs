using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.Domain.Endpoint;

/// <summary>
/// Routing context bound to one endpoint presence (M7.2-02 / M7.1 §15).
/// Stores corporate/internet/Wazuh route resolution traces for the presence interval.
/// </summary>
public sealed class EndpointRoutingContext : IEquatable<EndpointRoutingContext>
{
    public EndpointId EndpointId { get; }

    public PresenceId PresenceId { get; }

    public SiteId SiteId { get; }

    public NodeId NodeId { get; }

    public string? VlanId { get; }

    public string? Vrf { get; }

    public string SourceAddress { get; }

    public RouteResolutionTrace? CorporateRouteTrace { get; }

    public RouteResolutionTrace? InternetRouteTrace { get; }

    public RouteResolutionTrace? WazuhRouteTrace { get; }

    public DateTimeOffset ValidFrom { get; }

    public DateTimeOffset? ValidUntil { get; }

    private EndpointRoutingContext(
        EndpointId endpointId,
        PresenceId presenceId,
        SiteId siteId,
        NodeId nodeId,
        string? vlanId,
        string? vrf,
        string sourceAddress,
        RouteResolutionTrace? corporateRouteTrace,
        RouteResolutionTrace? internetRouteTrace,
        RouteResolutionTrace? wazuhRouteTrace,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil)
    {
        EndpointId = endpointId;
        PresenceId = presenceId;
        SiteId = siteId;
        NodeId = nodeId;
        VlanId = vlanId;
        Vrf = vrf;
        SourceAddress = sourceAddress;
        CorporateRouteTrace = corporateRouteTrace;
        InternetRouteTrace = internetRouteTrace;
        WazuhRouteTrace = wazuhRouteTrace;
        ValidFrom = validFrom.ToUniversalTime();
        ValidUntil = validUntil?.ToUniversalTime();
        ValidateValidityRange(ValidFrom, ValidUntil);
    }

    /// <summary>Creates routing context for a newly opened presence interval.</summary>
    public static EndpointRoutingContext Create(
        EndpointPresenceInterval interval,
        RouteResolutionTrace? corporateRouteTrace = null,
        RouteResolutionTrace? internetRouteTrace = null,
        RouteResolutionTrace? wazuhRouteTrace = null)
    {
        ArgumentNullException.ThrowIfNull(interval);
        return new EndpointRoutingContext(
            interval.EndpointId,
            interval.PresenceId,
            interval.SiteId,
            interval.NodeId,
            interval.VlanId,
            interval.Vrf,
            interval.SourceAddress,
            corporateRouteTrace,
            internetRouteTrace,
            wazuhRouteTrace,
            interval.ValidFrom,
            interval.ValidUntil);
    }

    /// <summary>Reconstitutes routing context from persistence.</summary>
    public static EndpointRoutingContext Reconstitute(
        EndpointId endpointId,
        PresenceId presenceId,
        SiteId siteId,
        NodeId nodeId,
        string sourceAddress,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        string? vlanId = null,
        string? vrf = null,
        RouteResolutionTrace? corporateRouteTrace = null,
        RouteResolutionTrace? internetRouteTrace = null,
        RouteResolutionTrace? wazuhRouteTrace = null)
        => new(
            endpointId,
            presenceId,
            siteId,
            nodeId,
            NormalizeOptional(vlanId),
            NormalizeOptional(vrf),
            sourceAddress.Trim(),
            corporateRouteTrace,
            internetRouteTrace,
            wazuhRouteTrace,
            validFrom,
            validUntil);

    public bool Contains(DateTimeOffset asOf)
    {
        DateTimeOffset point = asOf.ToUniversalTime();
        if (point < ValidFrom)
        {
            return false;
        }

        return ValidUntil is null || point < ValidUntil.Value;
    }

    public bool Equals(EndpointRoutingContext? other)
    {
        if (other is null)
        {
            return false;
        }

        return EndpointId.Equals(other.EndpointId)
               && PresenceId.Equals(other.PresenceId)
               && SiteId.Equals(other.SiteId)
               && NodeId.Equals(other.NodeId)
               && string.Equals(VlanId, other.VlanId, StringComparison.Ordinal)
               && string.Equals(Vrf, other.Vrf, StringComparison.Ordinal)
               && string.Equals(SourceAddress, other.SourceAddress, StringComparison.OrdinalIgnoreCase)
               && ValidFrom.Equals(other.ValidFrom)
               && Nullable.Equals(ValidUntil, other.ValidUntil);
    }

    public override bool Equals(object? obj) => obj is EndpointRoutingContext other && Equals(other);

    public override int GetHashCode() => PresenceId.GetHashCode();

    private static void ValidateValidityRange(DateTimeOffset validFrom, DateTimeOffset? validUntil)
    {
        if (validUntil is not null && validUntil.Value <= validFrom)
        {
            throw new DomainInvariantException(
                $"valid_until must be greater than valid_from ({EndpointPresenceCodes.InvalidValidityRange}).");
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
