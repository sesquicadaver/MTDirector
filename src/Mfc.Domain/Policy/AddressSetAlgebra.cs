using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>Interval-set algebra for address normalization and selectors (Policy Model §16.1 / §17).</summary>
public static class AddressSetAlgebra
{
    public static IReadOnlyList<AddressInterval> Normalize(IEnumerable<AddressInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        List<AddressInterval> list = intervals.ToList();
        if (list.Count == 0)
        {
            return [];
        }

        IpAddressFamily family = list[0].Family;
        if (list.Any(i => i.Family != family))
        {
            throw new DomainInvariantException("Cannot normalize mixed address-family intervals.");
        }

        list.Sort();
        List<AddressInterval> merged = [];
        AddressInterval current = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            AddressInterval next = list[i];
            bool overlapsOrAdjacent = next.Start <= current.End
                || (current.End < AddressInterval.MaxValue(family) && next.Start == current.End + 1);
            if (overlapsOrAdjacent)
            {
                UInt128 end = next.End > current.End ? next.End : current.End;
                current = new AddressInterval(family, current.Start, end);
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

    public static IReadOnlyList<AddressInterval> Union(
        IEnumerable<AddressInterval> left,
        IEnumerable<AddressInterval> right)
        => Normalize(left.Concat(right));

    public static IReadOnlyList<AddressInterval> Intersect(
        IReadOnlyList<AddressInterval> left,
        IReadOnlyList<AddressInterval> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Count == 0 || right.Count == 0)
        {
            return [];
        }

        EnsureSameFamily(left, right);
        List<AddressInterval> result = [];
        int i = 0;
        int j = 0;
        while (i < left.Count && j < right.Count)
        {
            AddressInterval a = left[i];
            AddressInterval b = right[j];
            UInt128 start = a.Start > b.Start ? a.Start : b.Start;
            UInt128 end = a.End < b.End ? a.End : b.End;
            if (start <= end)
            {
                result.Add(new AddressInterval(a.Family, start, end));
            }

            if (a.End < b.End)
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return Normalize(result);
    }

    /// <summary>Returns <paramref name="include"/> minus <paramref name="exclude"/>.</summary>
    public static IReadOnlyList<AddressInterval> Subtract(
        IReadOnlyList<AddressInterval> include,
        IReadOnlyList<AddressInterval> exclude)
    {
        ArgumentNullException.ThrowIfNull(include);
        ArgumentNullException.ThrowIfNull(exclude);
        if (include.Count == 0)
        {
            return [];
        }

        if (exclude.Count == 0)
        {
            return Normalize(include);
        }

        EnsureSameFamily(include, exclude);
        List<AddressInterval> current = Normalize(include).ToList();
        foreach (AddressInterval cut in Normalize(exclude))
        {
            List<AddressInterval> next = [];
            foreach (AddressInterval piece in current)
            {
                next.AddRange(SubtractOne(piece, cut));
            }

            current = next;
        }

        return Normalize(current);
    }

    public static bool IsSubset(
        IReadOnlyList<AddressInterval> candidate,
        IReadOnlyList<AddressInterval> cover)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(cover);
        IReadOnlyList<AddressInterval> left = Normalize(candidate);
        IReadOnlyList<AddressInterval> right = Normalize(cover);
        if (left.Count == 0)
        {
            return true;
        }

        if (right.Count == 0)
        {
            return false;
        }

        EnsureSameFamily(left, right);
        foreach (AddressInterval item in left)
        {
            UInt128 cursor = item.Start;
            while (true)
            {
                AddressInterval? match = null;
                foreach (AddressInterval c in right)
                {
                    if (c.Start <= cursor && cursor <= c.End)
                    {
                        match = c;
                        break;
                    }
                }

                if (match is null)
                {
                    return false;
                }

                AddressInterval covering = match.Value;
                if (covering.End >= item.End)
                {
                    break;
                }

                if (covering.End == AddressInterval.MaxValue(item.Family))
                {
                    return false;
                }

                cursor = covering.End + 1;
            }
        }

        return true;
    }

    private static IEnumerable<AddressInterval> SubtractOne(AddressInterval piece, AddressInterval cut)
    {
        if (cut.End < piece.Start || cut.Start > piece.End)
        {
            yield return piece;
            yield break;
        }

        if (cut.Start > piece.Start)
        {
            yield return new AddressInterval(piece.Family, piece.Start, cut.Start - 1);
        }

        if (cut.End < piece.End)
        {
            yield return new AddressInterval(piece.Family, cut.End + 1, piece.End);
        }
    }

    private static void EnsureSameFamily(
        IReadOnlyList<AddressInterval> left,
        IReadOnlyList<AddressInterval> right)
    {
        IpAddressFamily family = left[0].Family;
        if (left.Any(i => i.Family != family) || right.Any(i => i.Family != family))
        {
            throw new DomainInvariantException("Address-set operations require a single address family.");
        }
    }
}
