using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

/// <summary>
/// Property-based coverage for normalization / subset / intersection (M2-03 AC#12).
/// Deterministic seeded trials (no external PBT package in the solution yet).
/// </summary>
public sealed class AddressSetAlgebraPropertyTests
{
    [Fact]
    public void NormalizationIsIdempotentAndOrderIndependent()
    {
        Random rng = new(42);
        for (int trial = 0; trial < 200; trial++)
        {
            List<AddressInterval> raw = RandomIntervals(rng, count: rng.Next(1, 8));
            IReadOnlyList<AddressInterval> once = AddressSetAlgebra.Normalize(raw);
            IReadOnlyList<AddressInterval> twice = AddressSetAlgebra.Normalize(once);
            Assert.Equal(once, twice);

            List<AddressInterval> shuffled = raw.OrderBy(_ => rng.Next()).ToList();
            Assert.Equal(once, AddressSetAlgebra.Normalize(shuffled));
        }
    }

    [Fact]
    public void IntersectionIsSubsetOfBothOperands()
    {
        Random rng = new(7);
        for (int trial = 0; trial < 200; trial++)
        {
            IReadOnlyList<AddressInterval> a = AddressSetAlgebra.Normalize(RandomIntervals(rng, rng.Next(0, 6)));
            IReadOnlyList<AddressInterval> b = AddressSetAlgebra.Normalize(RandomIntervals(rng, rng.Next(0, 6)));
            IReadOnlyList<AddressInterval> inter = AddressSetAlgebra.Intersect(a, b);
            Assert.True(AddressSetAlgebra.IsSubset(inter, a));
            Assert.True(AddressSetAlgebra.IsSubset(inter, b));
        }
    }

    [Fact]
    public void SubtractThenIntersectWithExcludeIsEmpty()
    {
        Random rng = new(99);
        for (int trial = 0; trial < 200; trial++)
        {
            IReadOnlyList<AddressInterval> include = AddressSetAlgebra.Normalize(RandomIntervals(rng, rng.Next(1, 6)));
            IReadOnlyList<AddressInterval> exclude = AddressSetAlgebra.Normalize(RandomIntervals(rng, rng.Next(0, 6)));
            IReadOnlyList<AddressInterval> left = AddressSetAlgebra.Subtract(include, exclude);
            Assert.Empty(AddressSetAlgebra.Intersect(left, exclude));
            Assert.True(AddressSetAlgebra.IsSubset(left, include));
        }
    }

    private static List<AddressInterval> RandomIntervals(Random rng, int count)
    {
        List<AddressInterval> list = [];
        for (int i = 0; i < count; i++)
        {
            uint a = (uint)rng.Next(0, 50);
            uint b = (uint)rng.Next(0, 50);
            uint start = Math.Min(a, b);
            uint end = Math.Max(a, b);
            list.Add(new AddressInterval(IpAddressFamily.IPv4, start, end));
        }

        return list;
    }
}
