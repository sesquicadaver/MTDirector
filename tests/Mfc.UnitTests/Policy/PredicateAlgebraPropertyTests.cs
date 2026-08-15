using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

/// <summary>Deterministic algebraic identities (M2-09 AC#11). No FsCheck package.</summary>
public sealed class PredicateAlgebraPropertyTests
{
    [Fact]
    public void IntersectionIsIdempotentAndSubsetOfBoth()
    {
        Random rng = new(11);
        for (int trial = 0; trial < 80; trial++)
        {
            NormalizedPredicate a = RandomPredicate(rng);
            NormalizedPredicate b = RandomPredicate(rng);
            NormalizedPredicate inter = PredicateAlgebra.Intersect(a, a).Value!;
            Assert.True(PredicateAlgebra.IsSubset(inter, a));
            Assert.True(PredicateAlgebra.IsSubset(a, inter) || a.IsEmpty);

            NormalizedPredicate ab = PredicateAlgebra.Intersect(a, b).Value!;
            Assert.True(PredicateAlgebra.IsSubset(ab, a));
            Assert.True(PredicateAlgebra.IsSubset(ab, b));
        }
    }

    [Fact]
    public void UnionIsCommutativeAndSubtractSelfIsEmpty()
    {
        Random rng = new(23);
        for (int trial = 0; trial < 80; trial++)
        {
            NormalizedPredicate a = RandomPredicate(rng);
            NormalizedPredicate b = RandomPredicate(rng);
            NormalizedPredicate left = PredicateAlgebra.Union(a, b).Value!;
            NormalizedPredicate right = PredicateAlgebra.Union(b, a).Value!;
            Assert.True(PredicateAlgebra.IsSubset(left, right));
            Assert.True(PredicateAlgebra.IsSubset(right, left));

            NormalizedPredicate minus = PredicateAlgebra.Subtract(a, a).Value!;
            Assert.True(minus.IsEmpty);
        }
    }

    private static NormalizedPredicate RandomPredicate(Random rng)
    {
        uint start = (uint)rng.Next(0, 40);
        uint width = (uint)rng.Next(0, 20);
        AddressInterval interval = new(IpAddressFamily.IPv4, start, start + width);
        AtomicTrafficCube cube = AtomicTrafficCube.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            [interval],
            [AddressInterval.Universe(IpAddressFamily.IPv4)],
            protocols: rng.Next(2) == 0 ? ProtocolBitSet.Universe : ProtocolBitSet.Singleton((byte)rng.Next(1, 20)));
        return NormalizedPredicate.Create([cube]).Value!;
    }
}
