using System.Net;
using System.Net.Sockets;

namespace Mfc.Domain.Inventory.Primitives;

/// <summary>
/// IPv4/IPv6 prefix used for VRRP virtual addresses (not a free-form string).
/// </summary>
public sealed class AddressPrefix : IEquatable<AddressPrefix>
{
    public IPAddress Address { get; }

    public byte PrefixLength { get; }

    public IpAddressFamily Family { get; }

    private AddressPrefix(IPAddress address, byte prefixLength, IpAddressFamily family)
    {
        Address = address;
        PrefixLength = prefixLength;
        Family = family;
    }

    public static AddressPrefix Create(IPAddress address, byte prefixLength)
    {
        ArgumentNullException.ThrowIfNull(address);
        IpAddressFamily family = address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IpAddressFamily.IPv4,
            AddressFamily.InterNetworkV6 => IpAddressFamily.IPv6,
            _ => throw new DomainInvariantException("AddressPrefix supports only IPv4/IPv6."),
        };

        byte max = family == IpAddressFamily.IPv4 ? (byte)32 : (byte)128;
        if (prefixLength > max)
        {
            throw new DomainInvariantException($"Prefix length must be between 0 and {max}.");
        }

        return new AddressPrefix(address, prefixLength, family);
    }

    public static AddressPrefix Parse(string cidr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cidr);
        string[] parts = cidr.Trim().Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out IPAddress? address)
            || !byte.TryParse(parts[1], out byte prefix))
        {
            throw new DomainInvariantException("AddressPrefix must be in CIDR form, e.g. 10.0.0.1/24.");
        }

        return Create(address, prefix);
    }

    public bool Equals(AddressPrefix? other)
        => other is not null
           && Address.Equals(other.Address)
           && PrefixLength == other.PrefixLength;

    public override bool Equals(object? obj) => obj is AddressPrefix other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Address, PrefixLength);

    public override string ToString() => $"{Address}/{PrefixLength}";
}
