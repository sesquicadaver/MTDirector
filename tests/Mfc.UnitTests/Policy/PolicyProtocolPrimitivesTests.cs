using Mfc.Domain;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyProtocolPrimitivesTests
{
    [Fact]
    public void IcmpSelectorOrdersComparesAndDedupesSets()
    {
        IcmpSelector anyCode = new(8);
        IcmpSelector withCode = new(8, 0);
        IcmpSelector otherType = new(3);

        Assert.True(anyCode < withCode);
        Assert.True(withCode > anyCode);
        Assert.True(otherType < anyCode);
        Assert.True(anyCode.CompareTo(null) > 0);
        Assert.Equal(0, anyCode.CompareTo(new IcmpSelector(8)));
        Assert.NotEqual(anyCode, withCode);
        Assert.True(anyCode == new IcmpSelector(8));
        Assert.False(anyCode != new IcmpSelector(8));
        Assert.True(anyCode <= withCode);
        Assert.True(withCode >= anyCode);

        IcmpSelectorSet set = IcmpSelectorSet.Create(
        [
            new IcmpSelector(8, 0),
            new IcmpSelector(3),
            new IcmpSelector(8, 0),
        ]);
        Assert.Equal(2, set.Items.Count);
        Assert.Equal(new IcmpSelector(3), set.Items[0]);
        Assert.Equal(IcmpSelectorSet.Empty, IcmpSelectorSet.Create([]));
        Assert.Throws<ArgumentNullException>(() => IcmpSelectorSet.Create(null!));

        IcmpSelectorSet duplicate = IcmpSelectorSet.Create([new IcmpSelector(3), new IcmpSelector(8, 0)]);
        Assert.True(set.Equals(duplicate));
        Assert.Equal(set.GetHashCode(), duplicate.GetHashCode());
        Assert.False(set.Equals(null));
        Assert.False(set.Equals(new object()));
        Assert.False(new IcmpSelector(1) == null);
        Assert.False(null == new IcmpSelector(1));
        Assert.True(new IcmpSelector(1) != null);
        Assert.True(new IcmpSelector(1) > null);
        Assert.True(null < new IcmpSelector(1));
        Assert.False(new IcmpSelector(1).Equals(new object()));
    }

    [Fact]
    public void IpProtocolExposesSemanticsOrderingAndStringForms()
    {
        IpProtocol any = IpProtocol.Any;
        IpProtocol tcp = IpProtocol.Create(IpProtocol.Tcp, "tcp");
        IpProtocol udp = IpProtocol.Create(IpProtocol.Udp);
        IpProtocol icmp = IpProtocol.Create(IpProtocol.Icmp);
        IpProtocol icmp6 = IpProtocol.Create(IpProtocol.IcmpV6, "ipv6-icmp");
        IpProtocol gre = IpProtocol.Create(47, "gre");

        Assert.True(any.IsAny);
        Assert.True(tcp.HasPortSemantics);
        Assert.True(udp.HasPortSemantics);
        Assert.True(IpProtocol.Create(IpProtocol.Sctp).HasPortSemantics);
        Assert.False(icmp.HasPortSemantics);
        Assert.True(icmp.IsIcmpV4);
        Assert.True(icmp6.IsIcmpV6Protocol);
        Assert.False(gre.HasPortSemantics);

        Assert.Equal("any", any.ToString());
        Assert.Equal("17", udp.ToString());
        Assert.Equal("6/tcp", tcp.ToString());
        Assert.True(udp > tcp);
        Assert.True(any > tcp);
        Assert.True(tcp.CompareTo(null) > 0);
        Assert.Equal(0, tcp.CompareTo(IpProtocol.Create(IpProtocol.Tcp, "ignored-name")));
        Assert.True(tcp == IpProtocol.Create(IpProtocol.Tcp));
        Assert.True(tcp != udp);
        Assert.True(tcp <= IpProtocol.Create(IpProtocol.Tcp));
        Assert.True(udp >= tcp);
        Assert.False(tcp.Equals(new object()));
        Assert.False(tcp == null);
        Assert.False(null == tcp);
        Assert.True(null < tcp);
        Assert.True(tcp > null);
    }

    [Fact]
    public void ServiceTermValidatesPortsIcmpAndOrdering()
    {
        PortSet dst = PortSet.Create([new PortInterval(443, 443)]);
        PortSet src = PortSet.Create([new PortInterval(1024, 65535)]);
        IcmpSelectorSet icmp = IcmpSelectorSet.Create([new IcmpSelector(8)]);

        ServiceTerm tcp = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Tcp),
            sourcePorts: src,
            destinationPorts: dst);
        ServiceTerm icmpTerm = ServiceTerm.Create(IpProtocol.Create(IpProtocol.Icmp), icmpSelectors: icmp);
        ServiceTerm bareIcmp = ServiceTerm.Create(IpProtocol.Create(IpProtocol.IcmpV6));

        Assert.Equal(dst, tcp.DestinationPorts);
        Assert.Equal(src, tcp.SourcePorts);
        Assert.Null(bareIcmp.IcmpSelectors);

        Assert.Throws<ArgumentNullException>(() => ServiceTerm.Create(null!));
        Assert.Throws<DomainInvariantException>(() =>
            ServiceTerm.Create(IpProtocol.Any, destinationPorts: dst));
        Assert.Throws<DomainInvariantException>(() =>
            ServiceTerm.Create(IpProtocol.Create(IpProtocol.Icmp), destinationPorts: dst));
        Assert.Throws<DomainInvariantException>(() =>
            ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp), icmpSelectors: icmp));

        ServiceTerm tcpOtherPorts = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Tcp),
            destinationPorts: PortSet.Create([new PortInterval(80, 80)]));
        Assert.True(tcpOtherPorts < tcp);
        Assert.True(icmpTerm < tcpOtherPorts);
        Assert.True(tcp.CompareTo(null) > 0);
        Assert.Equal(0, tcp.CompareTo(ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Tcp),
            sourcePorts: src,
            destinationPorts: dst)));
        Assert.True(tcp == ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Tcp),
            sourcePorts: src,
            destinationPorts: dst));
        Assert.True(tcp != tcpOtherPorts);
        Assert.True(tcp > null);
        Assert.True(null < icmpTerm);
        Assert.False(tcp.Equals(new object()));

        ServiceTerm tcpWithSrc = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Tcp),
            sourcePorts: PortSet.Create([new PortInterval(1024, 65535)]));
        ServiceTerm tcpWithIcmp = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Icmp),
            icmpSelectors: IcmpSelectorSet.Create([new IcmpSelector(0), new IcmpSelector(3)]));
        ServiceTerm tcpWithOtherIcmp = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Icmp),
            icmpSelectors: IcmpSelectorSet.Create([new IcmpSelector(8)]));
        Assert.NotEqual(0, tcpWithSrc.CompareTo(tcpOtherPorts));
        Assert.NotEqual(0, tcpWithIcmp.CompareTo(tcpWithOtherIcmp));
        Assert.True(tcpWithIcmp >= tcpWithOtherIcmp || tcpWithIcmp <= tcpWithOtherIcmp);
    }
}
