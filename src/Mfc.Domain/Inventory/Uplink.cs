using System.Net;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Inventory;

/// <summary>
/// Uplink model used for firewall compilation/validation. Not a persisted aggregate in M1 (Vertical Slice §31).
/// </summary>
public sealed class Uplink
{
    public UplinkId Id { get; }

    public NodeId NodeId { get; }

    public NonEmptyName Key { get; }

    public UplinkTrafficMode Mode { get; private set; }

    public NonEmptyName ZoneKey { get; private set; }

    public string? RoutingTable { get; private set; }

    public IPAddress? SourceAddress { get; private set; }

    private Uplink(
        UplinkId id,
        NodeId nodeId,
        NonEmptyName key,
        UplinkTrafficMode mode,
        NonEmptyName zoneKey,
        string? routingTable,
        IPAddress? sourceAddress)
    {
        Id = id;
        NodeId = nodeId;
        Key = key;
        Mode = mode;
        ZoneKey = zoneKey;
        RoutingTable = routingTable;
        SourceAddress = sourceAddress;
    }

    public static Uplink Create(
        NodeId nodeId,
        NonEmptyName key,
        UplinkTrafficMode mode,
        NonEmptyName zoneKey,
        string? routingTable = null,
        IPAddress? sourceAddress = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(zoneKey);
        if (routingTable is not null && string.IsNullOrWhiteSpace(routingTable))
        {
            throw new DomainInvariantException("routing_table, when set, must be non-empty.");
        }

        return new Uplink(
            UplinkId.New(),
            nodeId,
            key,
            mode,
            zoneKey,
            routingTable?.Trim(),
            sourceAddress);
    }

    public void SetMode(UplinkTrafficMode mode) => Mode = mode;

    public void SetZoneKey(NonEmptyName zoneKey)
    {
        ArgumentNullException.ThrowIfNull(zoneKey);
        ZoneKey = zoneKey;
    }

    public void SetSourceAddress(IPAddress? sourceAddress) => SourceAddress = sourceAddress;
}
