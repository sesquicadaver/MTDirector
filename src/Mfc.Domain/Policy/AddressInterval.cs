using System.Net;
using System.Net.Sockets;
using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>
/// Inclusive IP address interval used after address-object normalization (Policy Model §16.1).
/// IPv4 values occupy the low 32 bits of <see cref="Start"/> / <see cref="End"/>.
/// </summary>
public readonly struct AddressInterval : IComparable<AddressInterval>, IEquatable<AddressInterval>
{
    public IpAddressFamily Family { get; }

    public UInt128 Start { get; }

    public UInt128 End { get; }

    public AddressInterval(IpAddressFamily family, UInt128 start, UInt128 end)
    {
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported address family '{family}'.");
        }

        UInt128 max = MaxValue(family);
        if (start > end)
        {
            throw new DomainInvariantException("Address interval start must be <= end.");
        }

        if (start > max || end > max)
        {
            throw new DomainInvariantException("Address interval exceeds family address space.");
        }

        Family = family;
        Start = start;
        End = end;
    }

    public static UInt128 MaxValue(IpAddressFamily family)
        => family == IpAddressFamily.IPv4 ? uint.MaxValue : UInt128.MaxValue;

    public static AddressInterval Universe(IpAddressFamily family)
        => new(family, UInt128.Zero, MaxValue(family));

    public static UInt128 ToNumeric(IPAddress address, IpAddressFamily expectedFamily)
    {
        ArgumentNullException.ThrowIfNull(address);
        IpAddressFamily family = address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IpAddressFamily.IPv4,
            AddressFamily.InterNetworkV6 => IpAddressFamily.IPv6,
            _ => throw new DomainInvariantException("Only IPv4/IPv6 addresses are supported."),
        };
        if (family != expectedFamily)
        {
            throw new DomainInvariantException(
                $"Address family mismatch: expected {expectedFamily}, got {family}.");
        }

        byte[] bytes = address.GetAddressBytes();
        if (family == IpAddressFamily.IPv4)
        {
            // Network order → numeric.
            return ((uint)bytes[0] << 24)
                   | ((uint)bytes[1] << 16)
                   | ((uint)bytes[2] << 8)
                   | bytes[3];
        }

        UInt128 value = UInt128.Zero;
        foreach (byte b in bytes)
        {
            value = (value << 8) | b;
        }

        return value;
    }

    public static IPAddress FromNumeric(IpAddressFamily family, UInt128 value)
    {
        if (family == IpAddressFamily.IPv4)
        {
            uint v = (uint)value;
            return new IPAddress(
            [
                (byte)(v >> 24),
                (byte)(v >> 16),
                (byte)(v >> 8),
                (byte)v,
            ]);
        }

        byte[] bytes = new byte[16];
        UInt128 cursor = value;
        for (int i = 15; i >= 0; i--)
        {
            bytes[i] = (byte)(cursor & 0xFF);
            cursor >>= 8;
        }

        return new IPAddress(bytes);
    }

    /// <summary>Masks host bits of an address for the given prefix length.</summary>
    public static UInt128 MaskHostBits(IpAddressFamily family, UInt128 address, int prefixLength)
    {
        int width = family == IpAddressFamily.IPv4 ? 32 : 128;
        if (prefixLength is < 0 || prefixLength > width)
        {
            throw new DomainInvariantException($"Prefix length must be between 0 and {width}.");
        }

        if (prefixLength == 0)
        {
            return UInt128.Zero;
        }

        if (prefixLength == width)
        {
            return address;
        }

        int hostBits = width - prefixLength;
        UInt128 hostMask = (UInt128.One << hostBits) - 1;
        return address & ~hostMask;
    }

    public static AddressInterval FromPrefix(IpAddressFamily family, IPAddress address, int prefixLength)
    {
        UInt128 network = MaskHostBits(family, ToNumeric(address, family), prefixLength);
        int width = family == IpAddressFamily.IPv4 ? 32 : 128;
        UInt128 end;
        if (prefixLength == width)
        {
            end = network;
        }
        else if (prefixLength == 0)
        {
            end = MaxValue(family);
        }
        else
        {
            int hostBits = width - prefixLength;
            UInt128 hostMask = (UInt128.One << hostBits) - 1;
            end = network | hostMask;
        }

        return new AddressInterval(family, network, end);
    }

    public int CompareTo(AddressInterval other)
    {
        if (Family != other.Family)
        {
            return Family.CompareTo(other.Family);
        }

        int start = Start.CompareTo(other.Start);
        return start != 0 ? start : End.CompareTo(other.End);
    }

    public bool Equals(AddressInterval other)
        => Family == other.Family && Start == other.Start && End == other.End;

    public override bool Equals(object? obj) => obj is AddressInterval other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Family, Start, End);

    public static bool operator ==(AddressInterval left, AddressInterval right) => left.Equals(right);

    public static bool operator !=(AddressInterval left, AddressInterval right) => !left.Equals(right);

    public static bool operator <(AddressInterval left, AddressInterval right) => left.CompareTo(right) < 0;

    public static bool operator >(AddressInterval left, AddressInterval right) => left.CompareTo(right) > 0;

    public static bool operator <=(AddressInterval left, AddressInterval right) => left.CompareTo(right) <= 0;

    public static bool operator >=(AddressInterval left, AddressInterval right) => left.CompareTo(right) >= 0;

    public override string ToString()
        => $"{FromNumeric(Family, Start)}-{FromNumeric(Family, End)}";
}
