using Mfc.Application.Policies;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ServiceSelectorVisibilityTests
{
    [Fact]
    public void VisibilityIsUuidScopedUpwardForbidden()
    {
        Guid siteId = Guid.NewGuid();
        Guid nodeId = Guid.NewGuid();
        ServiceObject siteObj = ServiceObject.Create(
            PolicyObjectOwnerScope.Site,
            siteId,
            null,
            NonEmptyName.Create("site-svc"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp))]);
        ServiceObject nodeObj = ServiceObject.Create(
            PolicyObjectOwnerScope.Node,
            nodeId,
            null,
            NonEmptyName.Create("node-svc"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Udp))]);

        AddressConsumerContext company = new() { Scope = PolicyObjectOwnerScope.Company };
        AddressConsumerContext site = new() { Scope = PolicyObjectOwnerScope.Site, OwnerId = siteId };
        AddressConsumerContext node = new()
        {
            Scope = PolicyObjectOwnerScope.Node,
            OwnerId = nodeId,
            SiteId = siteId,
        };

        Assert.False(ServiceObjectVisibility.CanReference(company, siteObj));
        Assert.False(ServiceObjectVisibility.CanReference(site, nodeObj));
        Assert.True(ServiceObjectVisibility.CanReference(site, siteObj));
        Assert.True(ServiceObjectVisibility.CanReference(node, siteObj));

        Dictionary<ServiceObjectId, ServiceObject> catalog = new()
        {
            [siteObj.Id] = siteObj,
            [nodeObj.Id] = nodeObj,
        };
        Assert.Throws<DomainInvariantException>(() =>
            ServiceSelectorEvaluator.Resolve(
                ServiceSelector.Create([siteObj.Id]),
                IpAddressFamily.IPv4,
                catalog,
                company));
    }

    [Fact]
    public void Ipv4RuleRejectsIcmpV6ServiceObject()
    {
        ServiceObject icmp6 = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("icmp6"),
            [
                ServiceTerm.Create(
                    IpProtocol.Create(IpProtocol.IcmpV6),
                    icmpSelectors: IcmpSelectorSet.Create([new IcmpSelector(128)])),
            ]);
        Dictionary<ServiceObjectId, ServiceObject> catalog = new() { [icmp6.Id] = icmp6 };
        Assert.Throws<DomainInvariantException>(() =>
            ServiceSelectorResolver.Resolve(
                ServiceSelector.Create([icmp6.Id]),
                IpAddressFamily.IPv4,
                catalog));
    }
}
