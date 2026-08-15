using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PredicateAlgebraTests
{
    [Fact]
    public void EmptyPredicateRelatesAsEmptyAndIsSubsetOfAnything()
    {
        NormalizedPredicate empty = NormalizedPredicate.Empty;
        NormalizedPredicate any = Cube(src: Universe());
        Assert.Equal(PredicateRelation.Empty, PredicateAlgebra.Relate(empty, empty));
        Assert.Equal(PredicateRelation.Subset, PredicateAlgebra.Relate(empty, any));
        Assert.True(PredicateAlgebra.IsSubset(empty, any));
        Assert.False(PredicateAlgebra.Overlaps(empty, any));
    }

    [Fact]
    public void EqualCubesAreEqualAndDisjointHostsAreDisjoint()
    {
        NormalizedPredicate a = Cube(src: Host("10.0.0.1"));
        NormalizedPredicate b = Cube(src: Host("10.0.0.1"));
        NormalizedPredicate c = Cube(src: Host("10.0.0.2"));
        Assert.Equal(PredicateRelation.Equal, PredicateAlgebra.Relate(a, b));
        Assert.Equal(PredicateRelation.Disjoint, PredicateAlgebra.Relate(a, c));
    }

    [Fact]
    public void HostInsidePrefixIsSubsetAndPartialOverlapIsDetected()
    {
        NormalizedPredicate host = Cube(src: Host("10.0.0.1"));
        NormalizedPredicate prefix = Cube(src: Prefix("10.0.0.0", 24));
        Assert.Equal(PredicateRelation.Subset, PredicateAlgebra.Relate(host, prefix));
        Assert.Equal(PredicateRelation.Superset, PredicateAlgebra.Relate(prefix, host));
        Assert.Equal(PredicateRelation.PartialOverlap, PredicateAlgebra.Relate(
            Cube(src: Range("10.0.0.1", "10.0.0.20")),
            Cube(src: Range("10.0.0.10", "10.0.0.30"))));
    }

    [Fact]
    public void TcpFlagContradictionAfterIntersectIsEmpty()
    {
        AtomicTrafficCube syn = AtomicTrafficCube.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            Universe(),
            Universe(),
            tcpFlags: TcpFlagConstraint.Create([TcpHeaderBit.Syn], []));
        AtomicTrafficCube notSyn = AtomicTrafficCube.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            Universe(),
            Universe(),
            tcpFlags: TcpFlagConstraint.Create([], [TcpHeaderBit.Syn]));
        Assert.Null(AtomicTrafficCube.Intersect(syn, notSyn));
    }

    [Fact]
    public void IdenticalTcpFlagsIntersectIsIdempotentAndOverlaps()
    {
        TcpFlagConstraint syn = TcpFlagConstraint.Create([TcpHeaderBit.Syn], []);
        AtomicTrafficCube cube = AtomicTrafficCube.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            Universe(),
            Universe(),
            tcpFlags: syn);
        AtomicTrafficCube? inter = AtomicTrafficCube.Intersect(cube, cube);
        Assert.NotNull(inter);
        Assert.False(inter!.IsEmpty);
        Assert.True(AtomicTrafficCube.Overlaps(cube, cube));
        Assert.Equal([TcpHeaderBit.Syn], inter.TcpFlags!.RequiredPresent);
        Assert.Empty(inter.TcpFlags.RequiredAbsent);
    }

    [Fact]
    public void ProtocolSpecificPortsAreDisjoint()
    {
        NormalizedPredicate tcp = Cube(src: Universe(), protocol: IpProtocol.Tcp, destPort: 80);
        NormalizedPredicate udp = Cube(src: Universe(), protocol: IpProtocol.Udp, destPort: 80);
        Assert.Equal(PredicateRelation.Disjoint, PredicateAlgebra.Relate(tcp, udp));
        Assert.False(PredicateAlgebra.Overlaps(tcp, udp));
    }

    [Fact]
    public void Ipv4AndIpv6NeverMix()
    {
        NormalizedPredicate v4 = Cube(src: Universe(), family: IpAddressFamily.IPv4);
        NormalizedPredicate v6 = NormalizedPredicate.Create(
        [
            AtomicTrafficCube.Create(
                IpAddressFamily.IPv6,
                PolicyFilterChain.Forward,
                [AddressInterval.Universe(IpAddressFamily.IPv6)],
                [AddressInterval.Universe(IpAddressFamily.IPv6)]),
        ]).Value!;
        Assert.Equal(PredicateRelation.Disjoint, PredicateAlgebra.Relate(v4, v6));
        Assert.Null(AtomicTrafficCube.Intersect(v4.Cubes[0], v6.Cubes[0]));
    }

    [Fact]
    public void ZoneUniverseIsNotSubsetOfFiniteSet()
    {
        Guid zone = Guid.Parse("11111111-1111-1111-1111-111111111111");
        NormalizedPredicate unconstrained = Cube(src: Host("10.0.0.1"));
        NormalizedPredicate zoned = NormalizedPredicate.Create(
        [
            AtomicTrafficCube.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                Host("10.0.0.1"),
                Universe(),
                ingressZones: SymbolicSets.Finite([zone])),
        ]).Value!;
        Assert.False(PredicateAlgebra.IsSubset(unconstrained, zoned));
        Assert.True(PredicateAlgebra.IsSubset(zoned, unconstrained));
    }

    [Fact]
    public void ComplexityLimitRejectsTooManyCubes()
    {
        List<AtomicTrafficCube> cubes = [];
        for (int i = 0; i < PredicateAlgebraCodes.MaxCubesPerRule + 1; i++)
        {
            cubes.Add(AtomicTrafficCube.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                Host("10.0.0.1"),
                Universe(),
                protocols: ProtocolBitSet.Singleton((byte)i)));
        }

        PredicateAlgebraResult result = NormalizedPredicate.Create(cubes, PredicateAlgebraCodes.MaxCubesPerRule);
        Assert.True(result.IsFailure);
        Assert.Equal(PredicateAlgebraCodes.ComplexityLimit, result.Code);
    }

    [Fact]
    public void SubtractSelfIsEmptyAndUnionIsCommutative()
    {
        NormalizedPredicate a = Cube(src: Prefix("10.0.0.0", 24));
        NormalizedPredicate b = Cube(src: Host("10.1.0.1"));
        PredicateAlgebraResult minus = PredicateAlgebra.Subtract(a, a);
        Assert.True(minus.IsSuccess);
        Assert.True(minus.Value!.IsEmpty);

        NormalizedPredicate left = PredicateAlgebra.Union(a, b).Value!;
        NormalizedPredicate right = PredicateAlgebra.Union(b, a).Value!;
        Assert.True(PredicateAlgebra.IsSubset(left, right));
        Assert.True(PredicateAlgebra.IsSubset(right, left));
    }

    private static NormalizedPredicate Cube(
        IReadOnlyList<AddressInterval> src,
        IpAddressFamily family = IpAddressFamily.IPv4,
        byte? protocol = null,
        ushort? destPort = null)
    {
        AtomicTrafficCube cube = AtomicTrafficCube.Create(
            family,
            PolicyFilterChain.Forward,
            src,
            Universe(family),
            protocols: protocol is byte p ? ProtocolBitSet.Singleton(p) : ProtocolBitSet.Universe,
            destinationPorts: destPort is ushort port ? [new PortInterval(port, port)] : null);
        return NormalizedPredicate.Create([cube]).Value!;
    }

    private static IReadOnlyList<AddressInterval> Universe(IpAddressFamily family = IpAddressFamily.IPv4)
        => [AddressInterval.Universe(family)];

    private static IReadOnlyList<AddressInterval> Host(string ip)
    {
        System.Net.IPAddress address = System.Net.IPAddress.Parse(ip);
        UInt128 n = AddressInterval.ToNumeric(address, IpAddressFamily.IPv4);
        return [new AddressInterval(IpAddressFamily.IPv4, n, n)];
    }

    private static IReadOnlyList<AddressInterval> Prefix(string ip, int length)
        => [AddressInterval.FromPrefix(IpAddressFamily.IPv4, System.Net.IPAddress.Parse(ip), length)];

    private static IReadOnlyList<AddressInterval> Range(string start, string end)
        =>
        [
            new AddressInterval(
                IpAddressFamily.IPv4,
                AddressInterval.ToNumeric(System.Net.IPAddress.Parse(start), IpAddressFamily.IPv4),
                AddressInterval.ToNumeric(System.Net.IPAddress.Parse(end), IpAddressFamily.IPv4)),
        ];
}
