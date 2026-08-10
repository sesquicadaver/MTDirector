namespace Mfc.Domain.Policy;

/// <summary>ICMP / ICMPv6 type[/code] selector (Policy Model §18).</summary>
public sealed class IcmpSelector : IEquatable<IcmpSelector>, IComparable<IcmpSelector>
{
    public byte Type { get; }

    /// <summary>Null means any code for the type.</summary>
    public byte? Code { get; }

    public IcmpSelector(byte type, byte? code = null)
    {
        Type = type;
        Code = code;
    }

    public int CompareTo(IcmpSelector? other)
    {
        if (other is null)
        {
            return 1;
        }

        int type = Type.CompareTo(other.Type);
        if (type != 0)
        {
            return type;
        }

        return Nullable.Compare(Code, other.Code);
    }

    public bool Equals(IcmpSelector? other)
        => other is not null && Type == other.Type && Code == other.Code;

    public override bool Equals(object? obj) => obj is IcmpSelector other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Type, Code);

    public static bool operator ==(IcmpSelector? left, IcmpSelector? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(IcmpSelector? left, IcmpSelector? right) => !(left == right);

    public static bool operator <(IcmpSelector? left, IcmpSelector? right)
        => left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator >(IcmpSelector? left, IcmpSelector? right)
        => right is null ? left is not null : left is not null && left.CompareTo(right) > 0;

    public static bool operator <=(IcmpSelector? left, IcmpSelector? right) => !(left > right);

    public static bool operator >=(IcmpSelector? left, IcmpSelector? right) => !(left < right);
}

/// <summary>Canonical ICMP selector set for one ServiceTerm.</summary>
public sealed class IcmpSelectorSet
{
    public IReadOnlyList<IcmpSelector> Items { get; }

    private IcmpSelectorSet(IReadOnlyList<IcmpSelector> items) => Items = items;

    public static IcmpSelectorSet Empty { get; } = new([]);

    public static IcmpSelectorSet Create(IEnumerable<IcmpSelector> selectors)
    {
        ArgumentNullException.ThrowIfNull(selectors);
        IcmpSelector[] ordered = selectors
            .Distinct()
            .OrderBy(static s => s)
            .ToArray();
        return new IcmpSelectorSet(ordered);
    }

    public bool Equals(IcmpSelectorSet? other)
        => other is not null && Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj) => obj is IcmpSelectorSet other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hc = default;
        foreach (IcmpSelector s in Items)
        {
            hc.Add(s);
        }

        return hc.ToHashCode();
    }
}
