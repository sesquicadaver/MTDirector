using System.Globalization;

namespace Mfc.Domain.Policy;

/// <summary>
/// IP protocol identity: semantics are defined by the numeric value (Policy Model §18).
/// Named protocol is display metadata only.
/// </summary>
public sealed class IpProtocol : IEquatable<IpProtocol>, IComparable<IpProtocol>
{
    public const byte Tcp = 6;

    public const byte Udp = 17;

    public const byte Sctp = 132;

    public const byte Icmp = 1;

    public const byte IcmpV6 = 58;

    /// <summary>Explicit "any" matcher (not IP protocol number 0).</summary>
    public bool IsAny { get; }

    public byte Number { get; }

    public string? CanonicalName { get; }

    private IpProtocol(bool isAny, byte number, string? canonicalName)
    {
        IsAny = isAny;
        Number = number;
        CanonicalName = canonicalName;
    }

    public static IpProtocol Any { get; } = new(isAny: true, number: 0, canonicalName: "any");

    public static IpProtocol Create(byte number, string? canonicalName = null)
        => new(isAny: false, number, canonicalName);

    public bool HasPortSemantics
        => !IsAny && Number is Tcp or Udp or Sctp;

    public bool IsIcmpV4 => !IsAny && Number == Icmp;

    public bool IsIcmpV6Protocol => !IsAny && Number == IcmpV6;

    public int CompareTo(IpProtocol? other)
    {
        if (other is null)
        {
            return 1;
        }

        int any = IsAny.CompareTo(other.IsAny);
        return any != 0 ? any : Number.CompareTo(other.Number);
    }

    public bool Equals(IpProtocol? other)
        => other is not null && IsAny == other.IsAny && Number == other.Number;

    public override bool Equals(object? obj) => obj is IpProtocol other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(IsAny, Number);

    public static bool operator ==(IpProtocol? left, IpProtocol? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(IpProtocol? left, IpProtocol? right) => !(left == right);

    public static bool operator <(IpProtocol? left, IpProtocol? right)
        => left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator >(IpProtocol? left, IpProtocol? right)
        => right is null ? left is not null : left is not null && left.CompareTo(right) > 0;

    public static bool operator <=(IpProtocol? left, IpProtocol? right) => !(left > right);

    public static bool operator >=(IpProtocol? left, IpProtocol? right) => !(left < right);

    public override string ToString()
        => IsAny
            ? "any"
            : CanonicalName is null
                ? Number.ToString(CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{Number}/{CanonicalName}");
}
