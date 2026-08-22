using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Endpoint;

/// <summary>
/// Temporal endpoint presence at Site/Node with L2/L3 anchors (M7.2-02 / M7.1 §15).
/// <see cref="ValidUntil"/> null means the interval is active.
/// </summary>
public sealed class EndpointPresenceInterval : IEquatable<EndpointPresenceInterval>
{
    public PresenceId PresenceId { get; }

    public EndpointId EndpointId { get; }

    public SiteId SiteId { get; }

    public NodeId NodeId { get; }

    public DeviceId? DeviceId { get; }

    public string? VlanId { get; }

    public string? Vrf { get; }

    public string SourceAddress { get; }

    public string? MacAddress { get; }

    public EndpointAttributionCertainty AttributionCertainty { get; }

    public DateTimeOffset ValidFrom { get; }

    public DateTimeOffset? ValidUntil { get; }

    public bool IsActive => ValidUntil is null;

    private EndpointPresenceInterval(
        PresenceId presenceId,
        EndpointId endpointId,
        SiteId siteId,
        NodeId nodeId,
        DeviceId? deviceId,
        string? vlanId,
        string? vrf,
        string sourceAddress,
        string? macAddress,
        EndpointAttributionCertainty attributionCertainty,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil)
    {
        PresenceId = presenceId;
        EndpointId = endpointId;
        SiteId = siteId;
        NodeId = nodeId;
        DeviceId = deviceId;
        VlanId = vlanId;
        Vrf = vrf;
        SourceAddress = sourceAddress;
        MacAddress = macAddress;
        AttributionCertainty = attributionCertainty;
        ValidFrom = validFrom.ToUniversalTime();
        ValidUntil = validUntil?.ToUniversalTime();
    }

    /// <summary>Creates a new active presence interval.</summary>
    public static EndpointPresenceInterval Create(
        PresenceId presenceId,
        EndpointId endpointId,
        SiteId siteId,
        NodeId nodeId,
        string sourceAddress,
        EndpointAttributionCertainty attributionCertainty,
        DateTimeOffset validFrom,
        DeviceId? deviceId = null,
        string? vlanId = null,
        string? vrf = null,
        string? macAddress = null)
    {
        ValidateAnchors(siteId, nodeId, sourceAddress);
        DateTimeOffset from = validFrom.ToUniversalTime();
        return new EndpointPresenceInterval(
            presenceId,
            endpointId,
            siteId,
            nodeId,
            deviceId,
            NormalizeOptional(vlanId),
            NormalizeOptional(vrf),
            sourceAddress.Trim(),
            NormalizeOptional(macAddress),
            attributionCertainty,
            from,
            validUntil: null);
    }

    /// <summary>Reconstitutes a persisted interval.</summary>
    public static EndpointPresenceInterval Reconstitute(
        PresenceId presenceId,
        EndpointId endpointId,
        SiteId siteId,
        NodeId nodeId,
        string sourceAddress,
        EndpointAttributionCertainty attributionCertainty,
        DateTimeOffset validFrom,
        DateTimeOffset? validUntil,
        DeviceId? deviceId = null,
        string? vlanId = null,
        string? vrf = null,
        string? macAddress = null)
    {
        ValidateAnchors(siteId, nodeId, sourceAddress);
        DateTimeOffset from = validFrom.ToUniversalTime();
        DateTimeOffset? until = validUntil?.ToUniversalTime();
        ValidateValidityRange(from, until);
        return new EndpointPresenceInterval(
            presenceId,
            endpointId,
            siteId,
            nodeId,
            deviceId,
            NormalizeOptional(vlanId),
            NormalizeOptional(vrf),
            sourceAddress.Trim(),
            NormalizeOptional(macAddress),
            attributionCertainty,
            from,
            until);
    }

    /// <summary>
    /// Closes the active interval at <paramref name="validUntil"/> (must be strictly after <see cref="ValidFrom"/>).
    /// </summary>
    public EndpointPresenceInterval Close(DateTimeOffset validUntil)
    {
        if (!IsActive)
        {
            throw new DomainInvariantException(
                $"Presence interval '{PresenceId}' is not active ({EndpointPresenceCodes.IntervalNotActive}).");
        }

        DateTimeOffset until = validUntil.ToUniversalTime();
        if (until <= ValidFrom)
        {
            throw new DomainInvariantException(
                $"Presence close time must be after valid_from ({EndpointPresenceCodes.CloseBeforeValidFrom}).");
        }

        return new EndpointPresenceInterval(
            PresenceId,
            EndpointId,
            SiteId,
            NodeId,
            DeviceId,
            VlanId,
            Vrf,
            SourceAddress,
            MacAddress,
            AttributionCertainty,
            ValidFrom,
            until);
    }

    /// <summary>
    /// Migration helper: closes the prior active interval (if any) and opens a new presence_id.
    /// </summary>
    public static EndpointPresenceMigrationResult Open(
        EndpointId endpointId,
        EndpointPresenceInterval? activeInterval,
        EndpointAttributionResult attribution,
        EndpointAttributionQuery query,
        DateTimeOffset validFrom,
        string? vrf = null)
    {
        ArgumentNullException.ThrowIfNull(attribution);
        ArgumentNullException.ThrowIfNull(query);

        EndpointPresenceInterval opened = EndpointPresenceBuilder.BuildInterval(
            endpointId,
            PresenceId.New(),
            attribution,
            query,
            validFrom,
            vrf);
        if (activeInterval is null)
        {
            return new EndpointPresenceMigrationResult(null, opened);
        }

        if (!activeInterval.IsActive)
        {
            throw new DomainInvariantException(
                $"Endpoint '{endpointId}' already has a closed interval marked active ({EndpointPresenceCodes.OverlappingActiveInterval}).");
        }

        if (!activeInterval.EndpointId.Equals(endpointId))
        {
            throw new DomainInvariantException("Active presence interval endpoint_id mismatch.");
        }

        DateTimeOffset from = validFrom.ToUniversalTime();
        if (from <= activeInterval.ValidFrom)
        {
            throw new DomainInvariantException(
                $"New presence valid_from must be after the active interval valid_from ({EndpointPresenceCodes.InvalidValidityRange}).");
        }

        EndpointPresenceInterval closed = activeInterval.Close(from);
        return new EndpointPresenceMigrationResult(closed, opened);
    }

    /// <summary>Returns true when <paramref name="asOf"/> falls inside [valid_from, valid_until).</summary>
    public bool Contains(DateTimeOffset asOf)
    {
        DateTimeOffset point = asOf.ToUniversalTime();
        if (point < ValidFrom)
        {
            return false;
        }

        return ValidUntil is null || point < ValidUntil.Value;
    }

    public bool Equals(EndpointPresenceInterval? other)
    {
        if (other is null)
        {
            return false;
        }

        return PresenceId.Equals(other.PresenceId)
               && EndpointId.Equals(other.EndpointId)
               && SiteId.Equals(other.SiteId)
               && NodeId.Equals(other.NodeId)
               && Nullable.Equals(DeviceId, other.DeviceId)
               && string.Equals(VlanId, other.VlanId, StringComparison.Ordinal)
               && string.Equals(Vrf, other.Vrf, StringComparison.Ordinal)
               && string.Equals(SourceAddress, other.SourceAddress, StringComparison.OrdinalIgnoreCase)
               && string.Equals(MacAddress, other.MacAddress, StringComparison.OrdinalIgnoreCase)
               && AttributionCertainty == other.AttributionCertainty
               && ValidFrom.Equals(other.ValidFrom)
               && Nullable.Equals(ValidUntil, other.ValidUntil);
    }

    public override bool Equals(object? obj) => obj is EndpointPresenceInterval other && Equals(other);

    public override int GetHashCode() => PresenceId.GetHashCode();

    private static void ValidateAnchors(SiteId siteId, NodeId nodeId, string sourceAddress)
    {
        if (siteId.Value == Guid.Empty)
        {
            throw new DomainInvariantException($"Site_id is required ({EndpointPresenceCodes.MissingSiteId}).");
        }

        if (nodeId.Value == Guid.Empty)
        {
            throw new DomainInvariantException($"Node_id is required ({EndpointPresenceCodes.MissingNodeId}).");
        }

        if (string.IsNullOrWhiteSpace(sourceAddress))
        {
            throw new DomainInvariantException(
                $"Source address is required ({EndpointPresenceCodes.MissingSourceAddress}).");
        }
    }

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

/// <summary>Result of closing a prior interval and opening a new presence (M7.2-02 migration).</summary>
public sealed record EndpointPresenceMigrationResult(
    EndpointPresenceInterval? ClosedInterval,
    EndpointPresenceInterval OpenedInterval);
