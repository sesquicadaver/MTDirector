namespace Mfc.Domain.Policy;

/// <summary>Interval-set algebra for TCP/UDP/SCTP ports (Policy Model §37.1).</summary>
public static class PortSetAlgebra
{
    public static PortSet Universe { get; } = PortSet.Create([new PortInterval(0, ushort.MaxValue)]);

    public static IReadOnlyList<PortInterval> Union(
        IEnumerable<PortInterval> left,
        IEnumerable<PortInterval> right)
        => PortSet.Normalize(left.Concat(right));

    public static IReadOnlyList<PortInterval> Intersect(
        IReadOnlyList<PortInterval> left,
        IReadOnlyList<PortInterval> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Count == 0 || right.Count == 0)
        {
            return [];
        }

        List<PortInterval> result = [];
        int i = 0;
        int j = 0;
        while (i < left.Count && j < right.Count)
        {
            PortInterval a = left[i];
            PortInterval b = right[j];
            ushort start = a.Start > b.Start ? a.Start : b.Start;
            ushort end = a.End < b.End ? a.End : b.End;
            if (start <= end)
            {
                result.Add(new PortInterval(start, end));
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

        return PortSet.Normalize(result);
    }

    /// <summary>Returns <paramref name="include"/> minus <paramref name="exclude"/>.</summary>
    public static IReadOnlyList<PortInterval> Subtract(
        IReadOnlyList<PortInterval> include,
        IReadOnlyList<PortInterval> exclude)
    {
        ArgumentNullException.ThrowIfNull(include);
        ArgumentNullException.ThrowIfNull(exclude);
        if (include.Count == 0)
        {
            return [];
        }

        if (exclude.Count == 0)
        {
            return PortSet.Normalize(include);
        }

        List<PortInterval> current = PortSet.Normalize(include).ToList();
        foreach (PortInterval cut in PortSet.Normalize(exclude))
        {
            List<PortInterval> next = [];
            foreach (PortInterval piece in current)
            {
                next.AddRange(SubtractOne(piece, cut));
            }

            current = next;
        }

        return PortSet.Normalize(current);
    }

    public static bool IsSubset(IReadOnlyList<PortInterval> candidate, IReadOnlyList<PortInterval> cover)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(cover);
        IReadOnlyList<PortInterval> left = PortSet.Normalize(candidate);
        IReadOnlyList<PortInterval> right = PortSet.Normalize(cover);
        if (left.Count == 0)
        {
            return true;
        }

        if (right.Count == 0)
        {
            return false;
        }

        foreach (PortInterval item in left)
        {
            int cursor = item.Start;
            while (true)
            {
                PortInterval? match = null;
                foreach (PortInterval c in right)
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

                PortInterval covering = match.Value;
                if (covering.End >= item.End)
                {
                    break;
                }

                if (covering.End == ushort.MaxValue)
                {
                    return false;
                }

                cursor = covering.End + 1;
            }
        }

        return true;
    }

    public static bool IsDisjoint(IReadOnlyList<PortInterval> left, IReadOnlyList<PortInterval> right)
        => Intersect(left, right).Count == 0;

    public static bool AreEqual(IReadOnlyList<PortInterval> left, IReadOnlyList<PortInterval> right)
        => PortSet.Normalize(left).SequenceEqual(PortSet.Normalize(right));

    private static IEnumerable<PortInterval> SubtractOne(PortInterval piece, PortInterval cut)
    {
        if (cut.End < piece.Start || cut.Start > piece.End)
        {
            yield return piece;
            yield break;
        }

        if (cut.Start > piece.Start)
        {
            yield return new PortInterval(piece.Start, (ushort)(cut.Start - 1));
        }

        if (cut.End < piece.End)
        {
            yield return new PortInterval((ushort)(cut.End + 1), piece.End);
        }
    }
}
