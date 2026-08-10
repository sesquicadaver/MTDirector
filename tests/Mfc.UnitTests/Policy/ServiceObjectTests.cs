using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ServiceObjectTests
{
    [Fact]
    public void ProtocolSemanticsUseNumericValueTcpUdpPortsSupported()
    {
        ServiceTerm http = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Tcp, "tcp"),
            destinationPorts: PortSet.Create([new PortInterval(80, 80), new PortInterval(443, 443)]));
        ServiceTerm dns = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Udp, "udp"),
            destinationPorts: PortSet.Create([new PortInterval(53, 53)]));

        ServiceObject obj = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("web-dns"),
            [dns, http]);

        Assert.Equal(2, obj.Terms.Count);
        Assert.Equal(IpProtocol.Tcp, obj.Terms[0].Protocol.Number);
        Assert.Equal(
            [new PortInterval(80, 80), new PortInterval(443, 443)],
            obj.Terms[0].DestinationPorts!.Intervals);
    }

    [Fact]
    public void PortMatcherWithoutPortCapableProtocolIsForbidden()
    {
        Assert.Throws<DomainInvariantException>(() =>
            ServiceTerm.Create(
                IpProtocol.Create(1, "icmp"),
                destinationPorts: PortSet.Create([new PortInterval(1, 1)])));
    }

    [Fact]
    public void ProtocolAnyWithPortsIsForbiddenEmptyObjectForbidden()
    {
        Assert.Throws<DomainInvariantException>(() =>
            ServiceTerm.Create(
                IpProtocol.Any,
                destinationPorts: PortSet.Create([new PortInterval(22, 22)])));
        Assert.Throws<DomainInvariantException>(() =>
            ServiceObject.Create(
                PolicyObjectOwnerScope.Company,
                null,
                null,
                NonEmptyName.Create("empty"),
                []));
    }

    [Fact]
    public void IcmpAndIcmpV6AreSeparatedWrongFamilySelectorRejected()
    {
        ServiceTerm icmp = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Icmp, "icmp"),
            icmpSelectors: IcmpSelectorSet.Create([new IcmpSelector(8)]));
        ServiceTerm icmp6 = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.IcmpV6, "ipv6-icmp"),
            icmpSelectors: IcmpSelectorSet.Create([new IcmpSelector(128)]));

        Assert.Throws<DomainInvariantException>(() =>
            ServiceObject.Create(
                PolicyObjectOwnerScope.Company,
                null,
                null,
                NonEmptyName.Create("mixed"),
                [icmp, icmp6]));

        ServiceObject v4 = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("icmp4"),
            [icmp]);
        Assert.Throws<DomainInvariantException>(() =>
            ServiceSelectorResolver.EnsureFamilyCompatible(v4, IpAddressFamily.IPv6));
    }

    [Fact]
    public void PortIntervalsNormalizeAndMergeDuplicatesCanonicalizedOrderIndependent()
    {
        ServiceTerm a = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Tcp),
            destinationPorts: PortSet.Create(
            [
                new PortInterval(443, 443),
                new PortInterval(80, 82),
                new PortInterval(81, 81),
            ]));
        ServiceTerm b = ServiceTerm.Create(
            IpProtocol.Create(IpProtocol.Tcp),
            destinationPorts: PortSet.Create(
            [
                new PortInterval(80, 82),
                new PortInterval(443, 443),
            ]));

        ServiceObject first = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("a"),
            [b, a, a]);
        ServiceObject second = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("b"),
            [a, b]);

        Assert.Equal(first.Terms, second.Terms);
        Assert.Single(first.Terms);
        Assert.Equal(
            [new PortInterval(80, 82), new PortInterval(443, 443)],
            first.Terms[0].DestinationPorts!.Intervals);
    }

    [Fact]
    public void ServiceSelectorHasNoNegationEmptyIncludeIsAnyProtocol()
    {
        ServiceSelector any = ServiceSelector.Create();
        Assert.True(any.MatchesAnyProtocol);

        ServiceSelectorResolveResult resolved = ServiceSelectorResolver.Resolve(
            any,
            IpAddressFamily.IPv4,
            new Dictionary<ServiceObjectId, ServiceObject>());
        Assert.True(resolved.IsAnyProtocol);
        Assert.Empty(resolved.Terms);
    }
}
