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

    /// <summary>
    /// True when <paramref name="other"/> is the same family and every address in
    /// <paramref name="other"/> is also in this prefix (longer-or-equal prefix length).
    /// </summary>
    public bool Contains(AddressPrefix other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Family != other.Family || other.PrefixLength < PrefixLength)
        {
            return false;
        }

        byte[] a = Address.GetAddressBytes();
        byte[] b = other.Address.GetAddressBytes();
        int bits = PrefixLength;
        int i = 0;
        while (bits >= 8)
        {
            if (a[i] != b[i])
            {
                return false;
            }

            i++;
            bits -= 8;
        }

        if (bits == 0)
        {
            return true;
        }

        int mask = 0xFF << (8 - bits);
        return (a[i] & mask) == (b[i] & mask);
    }

    /// <summary>Host containment: the address treated as /32 or /128.</summary>
    public bool Contains(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        IpAddressFamily family = address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IpAddressFamily.IPv4,
            AddressFamily.InterNetworkV6 => IpAddressFamily.IPv6,
            _ => throw new DomainInvariantException("AddressPrefix supports only IPv4/IPv6."),
        };

        byte hostBits = family == IpAddressFamily.IPv4 ? (byte)32 : (byte)128;
        return Contains(Create(address, hostBits));
    }

    public bool Equals(AddressPrefix? other)
        => other is not null
           && Address.Equals(other.Address)
           && PrefixLength == other.PrefixLength;

    public override bool Equals(object? obj) => obj is AddressPrefix other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Address, PrefixLength);

    public override string ToString() => $"{Address}/{PrefixLength}";
}
