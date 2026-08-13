using System.Net;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ObjectVisibilityScopeTests
{
    [Fact]
    public void AddressVisibilityCoversCompanyExceptionAndEnsureThrows()
    {
        Guid siteId = Guid.NewGuid();
        Guid nodeId = Guid.NewGuid();
        PolicyRevisionId revision = PolicyRevisionId.New();
        PolicyRevisionId otherRevision = PolicyRevisionId.New();

        AddressObject company = AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("corp"),
            IpAddressFamily.IPv4,
            [AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1"))]);
        AddressObject siteObj = AddressObject.Create(
            PolicyObjectOwnerScope.Site,
            siteId,
            null,
            NonEmptyName.Create("site"),
            IpAddressFamily.IPv4,
            [AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.2"))]);
        AddressObject nodeObj = AddressObject.Create(
            PolicyObjectOwnerScope.Node,
            nodeId,
            null,
            NonEmptyName.Create("node"),
            IpAddressFamily.IPv4,
            [AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.3"))]);
        AddressObject exceptionObj = AddressObject.Create(
            PolicyObjectOwnerScope.Exception,
            siteId,
            revision,
            NonEmptyName.Create("exc"),
            IpAddressFamily.IPv4,
            [AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.4"))]);

        AddressConsumerContext companyCtx = new() { Scope = PolicyObjectOwnerScope.Company };
        AddressConsumerContext siteCtx = new() { Scope = PolicyObjectOwnerScope.Site, OwnerId = siteId };
        AddressConsumerContext nodeCtx = new()
        {
            Scope = PolicyObjectOwnerScope.Node,
            OwnerId = nodeId,
            SiteId = siteId,
        };
        AddressConsumerContext exceptionCtx = new()
        {
            Scope = PolicyObjectOwnerScope.Exception,
            OwnerId = siteId,
            ExceptionRevisionId = revision,
        };
        AddressConsumerContext wrongRevisionCtx = new()
        {
            Scope = PolicyObjectOwnerScope.Exception,
            OwnerId = siteId,
            ExceptionRevisionId = otherRevision,
        };

        Assert.True(AddressObjectVisibility.CanReference(companyCtx, company));
        Assert.True(AddressObjectVisibility.CanReference(siteCtx, siteObj));
        Assert.True(AddressObjectVisibility.CanReference(nodeCtx, siteObj));
        Assert.True(AddressObjectVisibility.CanReference(nodeCtx, nodeObj));
        Assert.True(AddressObjectVisibility.CanReference(exceptionCtx, exceptionObj));
        Assert.False(AddressObjectVisibility.CanReference(companyCtx, siteObj));
        Assert.False(AddressObjectVisibility.CanReference(siteCtx, nodeObj));
        Assert.False(AddressObjectVisibility.CanReference(wrongRevisionCtx, exceptionObj));
        Assert.True(AddressObjectVisibility.CanReference(exceptionCtx, siteObj));

        AddressObjectVisibility.EnsureCanReference(siteCtx, siteObj);
        Assert.Throws<DomainInvariantException>(() =>
            AddressObjectVisibility.EnsureCanReference(companyCtx, siteObj));

        AddressConsumerContext exceptionNodeCtx = new()
        {
            Scope = PolicyObjectOwnerScope.Exception,
            OwnerId = nodeId,
            ExceptionRevisionId = revision,
        };
        Assert.True(AddressObjectVisibility.CanReference(exceptionNodeCtx, nodeObj));
        AddressObjectVisibility.EnsureCanReference(exceptionNodeCtx, nodeObj);
    }

    [Fact]
    public void ServiceVisibilityCoversExceptionScopeAndEnsureThrows()
    {
        Guid siteId = Guid.NewGuid();
        Guid nodeId = Guid.NewGuid();
        PolicyRevisionId revision = PolicyRevisionId.New();

        ServiceObject company = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("any-tcp"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp))]);
        ServiceObject siteObj = ServiceObject.Create(
            PolicyObjectOwnerScope.Site,
            siteId,
            null,
            NonEmptyName.Create("site-udp"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Udp))]);
        ServiceObject nodeObj = ServiceObject.Create(
            PolicyObjectOwnerScope.Node,
            nodeId,
            null,
            NonEmptyName.Create("node-gre"),
            [ServiceTerm.Create(IpProtocol.Create(47))]);
        ServiceObject exceptionObj = ServiceObject.Create(
            PolicyObjectOwnerScope.Exception,
            nodeId,
            revision,
            NonEmptyName.Create("exc-icmp"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Icmp))]);

        AddressConsumerContext companyCtx = new() { Scope = PolicyObjectOwnerScope.Company };
        AddressConsumerContext siteCtx = new() { Scope = PolicyObjectOwnerScope.Site, OwnerId = siteId };
        AddressConsumerContext nodeCtx = new()
        {
            Scope = PolicyObjectOwnerScope.Node,
            OwnerId = nodeId,
            SiteId = siteId,
        };
        AddressConsumerContext exceptionCtx = new()
        {
            Scope = PolicyObjectOwnerScope.Exception,
            OwnerId = nodeId,
            ExceptionRevisionId = revision,
        };

        Assert.True(ServiceObjectVisibility.CanReference(companyCtx, company));
        Assert.True(ServiceObjectVisibility.CanReference(siteCtx, siteObj));
        Assert.True(ServiceObjectVisibility.CanReference(nodeCtx, siteObj));
        Assert.False(ServiceObjectVisibility.CanReference(siteCtx, nodeObj));
        Assert.True(ServiceObjectVisibility.CanReference(exceptionCtx, exceptionObj));
        Assert.False(ServiceObjectVisibility.CanReference(nodeCtx, exceptionObj));

        ServiceObjectVisibility.EnsureCanReference(nodeCtx, siteObj);
        Assert.Throws<DomainInvariantException>(() =>
            ServiceObjectVisibility.EnsureCanReference(siteCtx, nodeObj));

        AddressConsumerContext exceptionNodeCtx = new()
        {
            Scope = PolicyObjectOwnerScope.Exception,
            OwnerId = nodeId,
            ExceptionRevisionId = revision,
        };
        Assert.True(ServiceObjectVisibility.CanReference(exceptionNodeCtx, nodeObj));
        ServiceObjectVisibility.EnsureCanReference(exceptionNodeCtx, nodeObj);
        Assert.Throws<DomainInvariantException>(() =>
            ServiceObjectVisibility.EnsureCanReference(exceptionNodeCtx, siteObj));
    }
}
