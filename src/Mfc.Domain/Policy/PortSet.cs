using System.Globalization;

namespace Mfc.Domain.Policy;

/// <summary>Inclusive TCP/UDP/SCTP port interval (0–65535).</summary>
public readonly struct PortInterval : IComparable<PortInterval>, IEquatable<PortInterval>
{
    public ushort Start { get; }

    public ushort End { get; }

    public PortInterval(ushort start, ushort end)
    {
        if (start > end)
        {
            throw new DomainInvariantException("Port interval start must be <= end.");
        }

        Start = start;
        End = end;
    }

    public int CompareTo(PortInterval other)
    {
        int start = Start.CompareTo(other.Start);
        return start != 0 ? start : End.CompareTo(other.End);
    }

    public bool Equals(PortInterval other) => Start == other.Start && End == other.End;

    public override bool Equals(object? obj) => obj is PortInterval other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Start, End);

    public static bool operator ==(PortInterval left, PortInterval right) => left.Equals(right);

    public static bool operator !=(PortInterval left, PortInterval right) => !left.Equals(right);

    public static bool operator <(PortInterval left, PortInterval right) => left.CompareTo(right) < 0;

    public static bool operator >(PortInterval left, PortInterval right) => left.CompareTo(right) > 0;

    public static bool operator <=(PortInterval left, PortInterval right) => left.CompareTo(right) <= 0;

    public static bool operator >=(PortInterval left, PortInterval right) => left.CompareTo(right) >= 0;

    public override string ToString()
        => Start == End
            ? Start.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{Start}-{End}");
}

/// <summary>Normalized disjoint port set.</summary>
public sealed class PortSet
{
    public IReadOnlyList<PortInterval> Intervals { get; }

    private PortSet(IReadOnlyList<PortInterval> intervals) => Intervals = intervals;

    public static PortSet Empty { get; } = new([]);

    public static PortSet Create(IEnumerable<PortInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        return new PortSet(Normalize(intervals));
    }

    public static IReadOnlyList<PortInterval> Normalize(IEnumerable<PortInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        List<PortInterval> list = intervals.ToList();
        if (list.Count == 0)
        {
            return [];
        }

        list.Sort();
        List<PortInterval> merged = [];
        PortInterval current = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            PortInterval next = list[i];
            bool overlapsOrAdjacent = next.Start <= current.End
                || (current.End < ushort.MaxValue && next.Start == current.End + 1);
            if (overlapsOrAdjacent)
            {
                ushort end = next.End > current.End ? next.End : current.End;
                current = new PortInterval(current.Start, end);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }

    public bool Equals(PortSet? other)
        => other is not null && Intervals.SequenceEqual(other.Intervals);

    public override bool Equals(object? obj) => obj is PortSet other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hc = default;
        foreach (PortInterval i in Intervals)
        {
            hc.Add(i);
        }

        return hc.ToHashCode();
    }
}
