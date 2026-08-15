using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class SymbolicSetTests
{
    [Fact]
    public void IntersectSubtractAndSubsetCoverUniverseAndFinite()
    {
        SymbolicSet<int> universe = SymbolicSets.Universe<int>();
        SymbolicSet<int> empty = SymbolicSets.Empty<int>();
        SymbolicSet<int> a = SymbolicSets.Finite([1, 2, 3]);
        SymbolicSet<int> b = SymbolicSets.Finite([3, 4]);
        SymbolicSet<int> minus = SymbolicSets.UniverseMinus([1, 2]);

        Assert.True(universe.IsUniverse);
        Assert.True(empty.IsEmpty);
        Assert.True(universe.Intersect(minus).Equals(minus));
        Assert.Equal(new HashSet<int> { 3 }, a.Intersect(b).Members.ToHashSet());
        Assert.Equal(new HashSet<int> { 3 }, minus.Intersect(a).Members.ToHashSet());
        Assert.Equal(new HashSet<int> { 3 }, a.Intersect(minus).Members.ToHashSet());

        Assert.True(empty.Subtract(a).IsEmpty);
        Assert.True(a.Subtract(empty).Equals(a));
        Assert.True(a.Subtract(universe).IsEmpty);
        Assert.Equal(new HashSet<int> { 1, 2 }, a.Subtract(b).Members.ToHashSet());
        Assert.True(universe.Subtract(a).IsUniverse);
        Assert.True(universe.Subtract(a).Members.SetEquals([1, 2, 3]));
        Assert.True(minus.Subtract(universe).IsEmpty);
        Assert.Equal(new HashSet<int> { 1, 2 }, universe.Subtract(minus).Members.ToHashSet());

        Assert.True(empty.IsSubsetOf(a));
        Assert.False(a.IsSubsetOf(empty));
        Assert.True(a.IsSubsetOf(universe));
        Assert.False(universe.IsSubsetOf(a));
        Assert.True(minus.IsSubsetOf(universe));
        Assert.False(minus.IsSubsetOf(a));
        Assert.True(a.Overlaps(b));
        Assert.False(SymbolicSets.Finite([9]).Overlaps(b));
        Assert.True(universe.Equals(SymbolicSets.Universe<int>()));
        Assert.False(a.Equals(b));
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void FromNullableListTreatsNullAndEmptyAsUniverse()
    {
        Assert.True(SymbolicSets.FromNullableList<int>(null).IsUniverse);
        Assert.True(SymbolicSets.FromNullableList<int>([]).IsUniverse);
        Assert.Equal(new HashSet<int> { 7 }, SymbolicSets.FromNullableList<int>([7]).Members.ToHashSet());
        Assert.True(SymbolicSets.Finite<int>([]).IsEmpty);
        Assert.True(SymbolicSets.UniverseMinus<int>([]).IsUniverse);
    }
}
