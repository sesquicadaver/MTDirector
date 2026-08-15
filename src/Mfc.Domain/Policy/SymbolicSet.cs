namespace Mfc.Domain.Policy;

/// <summary>
/// Finite set or symbolic universe-minus-exclude (Policy Model §37.1 bit/ID sets).
/// Unconstrained universe is never expanded to a catalog.
/// </summary>
public sealed class SymbolicSet<T> : IEquatable<SymbolicSet<T>>
    where T : notnull
{
    internal SymbolicSet(bool isUniverse, IReadOnlySet<T> members)
    {
        IsUniverse = isUniverse;
        Members = members;
    }

    public bool IsUniverse { get; }

    /// <summary>Finite members when <see cref="IsUniverse"/> is false; exclude set when true.</summary>
    public IReadOnlySet<T> Members { get; }

    public bool IsEmpty => !IsUniverse && Members.Count == 0;

    public SymbolicSet<T> Intersect(SymbolicSet<T> right)
    {
        ArgumentNullException.ThrowIfNull(right);
        if (IsUniverse && right.IsUniverse)
        {
            return SymbolicSets.UniverseMinus(Members.Union(right.Members));
        }

        if (IsUniverse)
        {
            return SymbolicSets.Finite(right.Members.Where(v => !Members.Contains(v)));
        }

        if (right.IsUniverse)
        {
            return SymbolicSets.Finite(Members.Where(v => !right.Members.Contains(v)));
        }

        return SymbolicSets.Finite(Members.Intersect(right.Members));
    }

    public SymbolicSet<T> Subtract(SymbolicSet<T> exclude)
    {
        ArgumentNullException.ThrowIfNull(exclude);
        if (IsEmpty)
        {
            return SymbolicSets.Empty<T>();
        }

        if (exclude.IsEmpty)
        {
            return this;
        }

        if (exclude.IsUniverse)
        {
            return IsUniverse
                ? SymbolicSets.Finite(exclude.Members.Where(v => !Members.Contains(v)))
                : SymbolicSets.Finite(Members.Intersect(exclude.Members));
        }

        if (IsUniverse)
        {
            return SymbolicSets.UniverseMinus(Members.Union(exclude.Members));
        }

        return SymbolicSets.Finite(Members.Except(exclude.Members));
    }

    public bool IsSubsetOf(SymbolicSet<T> cover)
    {
        ArgumentNullException.ThrowIfNull(cover);
        if (IsEmpty)
        {
            return true;
        }

        if (cover.IsEmpty)
        {
            return false;
        }

        if (IsUniverse)
        {
            return cover.IsUniverse && Members.IsSupersetOf(cover.Members);
        }

        if (cover.IsUniverse)
        {
            return Members.All(v => !cover.Members.Contains(v));
        }

        return Members.IsSubsetOf(cover.Members);
    }

    public bool Overlaps(SymbolicSet<T> right) => !Intersect(right).IsEmpty;

    public bool Equals(SymbolicSet<T>? other)
        => other is not null && IsUniverse == other.IsUniverse && Members.SetEquals(other.Members);

    public override bool Equals(object? obj) => obj is SymbolicSet<T> other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hc = default;
        hc.Add(IsUniverse);
        foreach (T item in Members.OrderBy(static v => v, Comparer<T>.Default))
        {
            hc.Add(item);
        }

        return hc.ToHashCode();
    }
}

/// <summary>Factories for <see cref="SymbolicSet{T}"/> (CA1000: keep statics off the generic type).</summary>
public static class SymbolicSets
{
    public static SymbolicSet<T> Universe<T>()
        where T : notnull
        => new(true, new HashSet<T>());

    public static SymbolicSet<T> Empty<T>()
        where T : notnull
        => new(false, new HashSet<T>());

    public static SymbolicSet<T> Finite<T>(IEnumerable<T> members)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(members);
        HashSet<T> set = members.ToHashSet();
        return set.Count == 0 ? Empty<T>() : new SymbolicSet<T>(false, set);
    }

    public static SymbolicSet<T> UniverseMinus<T>(IEnumerable<T> exclude)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(exclude);
        HashSet<T> set = exclude.ToHashSet();
        return set.Count == 0 ? Universe<T>() : new SymbolicSet<T>(true, set);
    }

    public static SymbolicSet<T> FromNullableList<T>(IReadOnlyList<T>? values)
        where T : notnull
        => values is null || values.Count == 0 ? Universe<T>() : Finite(values);
}
