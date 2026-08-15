using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PortSetAlgebraTests
{
    [Fact]
    public void UnionMergesAdjacentAndIntersectIsSubset()
    {
        IReadOnlyList<PortInterval> left = [new PortInterval(10, 20)];
        IReadOnlyList<PortInterval> right = [new PortInterval(21, 30)];
        IReadOnlyList<PortInterval> union = PortSetAlgebra.Union(left, right);
        Assert.Equal([new PortInterval(10, 30)], union);

        IReadOnlyList<PortInterval> inter = PortSetAlgebra.Intersect(
            [new PortInterval(10, 25)],
            [new PortInterval(20, 40)]);
        Assert.Equal([new PortInterval(20, 25)], inter);
        Assert.True(PortSetAlgebra.IsSubset(inter, [new PortInterval(10, 25)]));
    }

    [Fact]
    public void SubtractAndDisjoint()
    {
        IReadOnlyList<PortInterval> left = PortSetAlgebra.Subtract(
            [new PortInterval(1, 10)],
            [new PortInterval(4, 6)]);
        Assert.Equal([new PortInterval(1, 3), new PortInterval(7, 10)], left);
        Assert.True(PortSetAlgebra.IsDisjoint([new PortInterval(1, 3)], [new PortInterval(7, 10)]));
        Assert.True(PortSetAlgebra.AreEqual([new PortInterval(1, 1), new PortInterval(1, 1)], [new PortInterval(1, 1)]));
    }
}
