namespace Mfc.Domain.Policy;

/// <summary>256-bit IP protocol set (Policy Model §37.1). Universe is all 256 numbers.</summary>
public readonly struct ProtocolBitSet : IEquatable<ProtocolBitSet>
{
    private readonly ulong _w0;
    private readonly ulong _w1;
    private readonly ulong _w2;
    private readonly ulong _w3;

    private ProtocolBitSet(ulong w0, ulong w1, ulong w2, ulong w3)
    {
        _w0 = w0;
        _w1 = w1;
        _w2 = w2;
        _w3 = w3;
    }

    public static ProtocolBitSet Empty { get; } = new(0, 0, 0, 0);

    public static ProtocolBitSet Universe { get; } = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

    public bool IsEmpty => _w0 == 0 && _w1 == 0 && _w2 == 0 && _w3 == 0;

    public bool IsUniverse
        => _w0 == ulong.MaxValue
           && _w1 == ulong.MaxValue
           && _w2 == ulong.MaxValue
           && _w3 == ulong.MaxValue;

    public static ProtocolBitSet Singleton(byte number)
    {
        int word = number / 64;
        ulong mask = 1UL << (number % 64);
        return word switch
        {
            0 => new ProtocolBitSet(mask, 0, 0, 0),
            1 => new ProtocolBitSet(0, mask, 0, 0),
            2 => new ProtocolBitSet(0, 0, mask, 0),
            _ => new ProtocolBitSet(0, 0, 0, mask),
        };
    }

    public static ProtocolBitSet From(IpProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        return protocol.IsAny ? Universe : Singleton(protocol.Number);
    }

    public static ProtocolBitSet Intersect(ProtocolBitSet left, ProtocolBitSet right)
        => new(left._w0 & right._w0, left._w1 & right._w1, left._w2 & right._w2, left._w3 & right._w3);

    public static ProtocolBitSet Union(ProtocolBitSet left, ProtocolBitSet right)
        => new(left._w0 | right._w0, left._w1 | right._w1, left._w2 | right._w2, left._w3 | right._w3);

    public static ProtocolBitSet Subtract(ProtocolBitSet include, ProtocolBitSet exclude)
        => new(
            include._w0 & ~exclude._w0,
            include._w1 & ~exclude._w1,
            include._w2 & ~exclude._w2,
            include._w3 & ~exclude._w3);

    public static bool IsSubset(ProtocolBitSet inner, ProtocolBitSet cover)
        => Intersect(inner, cover).Equals(inner);

    public static bool Overlaps(ProtocolBitSet left, ProtocolBitSet right)
        => !Intersect(left, right).IsEmpty;

    public bool Contains(byte number)
    {
        int word = number / 64;
        ulong mask = 1UL << (number % 64);
        return word switch
        {
            0 => (_w0 & mask) != 0,
            1 => (_w1 & mask) != 0,
            2 => (_w2 & mask) != 0,
            _ => (_w3 & mask) != 0,
        };
    }

    public bool Equals(ProtocolBitSet other)
        => _w0 == other._w0 && _w1 == other._w1 && _w2 == other._w2 && _w3 == other._w3;

    public override bool Equals(object? obj) => obj is ProtocolBitSet other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_w0, _w1, _w2, _w3);

    public static bool operator ==(ProtocolBitSet left, ProtocolBitSet right) => left.Equals(right);

    public static bool operator !=(ProtocolBitSet left, ProtocolBitSet right) => !left.Equals(right);
}
