using System.Net;
using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>Static address entry kinds allowed in managed policy v1 (Policy Model §16).</summary>
public enum AddressEntryKind : byte
{
    Host = 0,
    Prefix = 1,
    Range = 2,
}

/// <summary>
/// Typed address entry. FQDN/dynamic/timeout entries are impossible by construction.
/// </summary>
public sealed class AddressEntry
{
    public AddressEntryKind Kind { get; }

    public IpAddressFamily Family { get; }

    public IPAddress? HostOrPrefixAddress { get; }

    public byte? PrefixLength { get; }

    public IPAddress? RangeStart { get; }

    public IPAddress? RangeEnd { get; }

    private AddressEntry(
        AddressEntryKind kind,
        IpAddressFamily family,
        IPAddress? hostOrPrefixAddress,
        byte? prefixLength,
        IPAddress? rangeStart,
        IPAddress? rangeEnd)
    {
        Kind = kind;
        Family = family;
        HostOrPrefixAddress = hostOrPrefixAddress;
        PrefixLength = prefixLength;
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;
    }

    public static AddressEntry Host(IpAddressFamily family, IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _ = AddressInterval.ToNumeric(address, family);
        return new AddressEntry(AddressEntryKind.Host, family, address, prefixLength: null, null, null);
    }

    public static AddressEntry Prefix(IpAddressFamily family, IPAddress address, byte prefixLength)
    {
        ArgumentNullException.ThrowIfNull(address);
        int width = family == IpAddressFamily.IPv4 ? 32 : 128;
        if (prefixLength > width)
        {
            throw new DomainInvariantException($"Prefix length must be between 0 and {width}.");
        }

        _ = AddressInterval.ToNumeric(address, family);
        return new AddressEntry(AddressEntryKind.Prefix, family, address, prefixLength, null, null);
    }

    public static AddressEntry Range(IpAddressFamily family, IPAddress start, IPAddress end)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        if (family != IpAddressFamily.IPv4)
        {
            throw new DomainInvariantException("RANGE entries are allowed only for IPv4 in managed policy v1.");
        }

        UInt128 a = AddressInterval.ToNumeric(start, family);
        UInt128 b = AddressInterval.ToNumeric(end, family);
        if (a > b)
        {
            throw new DomainInvariantException("IPv4 range start must be <= end.");
        }

        return new AddressEntry(AddressEntryKind.Range, family, null, null, start, end);
    }

    /// <summary>Converts this entry to a single inclusive interval (prefix host bits masked).</summary>
    public AddressInterval ToInterval()
        => Kind switch
        {
            AddressEntryKind.Host => new AddressInterval(
                Family,
                AddressInterval.ToNumeric(HostOrPrefixAddress!, Family),
                AddressInterval.ToNumeric(HostOrPrefixAddress!, Family)),
            AddressEntryKind.Prefix => AddressInterval.FromPrefix(
                Family,
                HostOrPrefixAddress!,
                PrefixLength!.Value),
            AddressEntryKind.Range => new AddressInterval(
                Family,
                AddressInterval.ToNumeric(RangeStart!, Family),
                AddressInterval.ToNumeric(RangeEnd!, Family)),
            _ => throw new DomainInvariantException($"Unknown address entry kind '{Kind}'."),
        };
}
