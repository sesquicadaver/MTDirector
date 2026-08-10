using System.Net;
using Mfc.Application.Policies;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class AddressSelectorTests
{
    [Fact]
    public void EmptyIncludeMeansUniverseMinusExclusions()
    {
        AddressObject deny = CompanyObj(
            "deny",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        Dictionary<AddressObjectId, AddressObject> catalog = new() { [deny.Id] = deny };

        AddressSelectorResolveResult result = AddressSelectorResolver.Resolve(
            AddressSelector.Create(include: null, exclude: [deny.Id]),
            IpAddressFamily.IPv4,
            catalog);

        Assert.False(result.IsUnsatisfiable);
        Assert.Equal(2, result.Intervals.Count);
        Assert.Equal(UInt128.Zero, result.Intervals[0].Start);
        Assert.Equal(
            AddressInterval.ToNumeric(IPAddress.Parse("10.0.0.1"), IpAddressFamily.IPv4) - 1,
            result.Intervals[0].End);
    }

    [Fact]
    public void UniverseMinusEverythingIsUnsatisfiableBlocker()
    {
        AddressObject all = CompanyObj(
            "all",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("0.0.0.0"), 0));
        Dictionary<AddressObjectId, AddressObject> catalog = new() { [all.Id] = all };

        AddressSelectorResolveResult result = AddressSelectorResolver.Resolve(
            AddressSelector.Create(include: null, exclude: [all.Id]),
            IpAddressFamily.IPv4,
            catalog);

        Assert.True(result.IsUnsatisfiable);
        Assert.Equal(AddressSelectorResolveResult.UnsatisfiableCode, "RULE_UNSATISFIABLE");
    }

    [Fact]
    public void DuplicateIdsAreRejectedAndIncludeExcludeIntersectionNormalizes()
    {
        AddressObjectId id = AddressObjectId.New();
        Assert.Throws<DomainInvariantException>(() =>
            AddressSelector.Create(include: [id, id], exclude: null));

        AddressObject net = CompanyObj(
            "net",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 24));
        Dictionary<AddressObjectId, AddressObject> catalog = new() { [net.Id] = net };
        AddressSelectorResolveResult result = AddressSelectorResolver.Resolve(
            AddressSelector.Create(include: [net.Id], exclude: [net.Id]),
            IpAddressFamily.IPv4,
            catalog);
        Assert.True(result.IsUnsatisfiable);
    }

    [Fact]
    public void InlineIpInManagedRuleIsForbidden()
    {
        Assert.Throws<DomainInvariantException>(() =>
            ManagedRuleAddressConstraint.EnsureNoInlineAddress(hasInlineIpLiteral: true));
        ManagedRuleAddressConstraint.EnsureNoInlineAddress(hasInlineIpLiteral: false);
    }

    [Fact]
    public void VisibilityIsUuidScopedUpwardReferencesForbidden()
    {
        Guid siteId = Guid.NewGuid();
        Guid nodeId = Guid.NewGuid();
        AddressObject siteObj = AddressObject.Create(
            PolicyObjectOwnerScope.Site,
            siteId,
            null,
            NonEmptyName.Create("site-obj"),
            IpAddressFamily.IPv4,
            [AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1"))]);
        AddressObject nodeObj = AddressObject.Create(
            PolicyObjectOwnerScope.Node,
            nodeId,
            null,
            NonEmptyName.Create("node-obj"),
            IpAddressFamily.IPv4,
            [AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.2"))]);

        AddressConsumerContext company = new() { Scope = PolicyObjectOwnerScope.Company };
        AddressConsumerContext site = new() { Scope = PolicyObjectOwnerScope.Site, OwnerId = siteId };
        AddressConsumerContext node = new()
        {
            Scope = PolicyObjectOwnerScope.Node,
            OwnerId = nodeId,
            SiteId = siteId,
        };

        Assert.False(AddressObjectVisibility.CanReference(company, siteObj));
        Assert.False(AddressObjectVisibility.CanReference(site, nodeObj));
        Assert.True(AddressObjectVisibility.CanReference(site, siteObj));
        Assert.True(AddressObjectVisibility.CanReference(node, siteObj));
        Assert.True(AddressObjectVisibility.CanReference(node, nodeObj));

        Dictionary<AddressObjectId, AddressObject> catalog = new()
        {
            [siteObj.Id] = siteObj,
            [nodeObj.Id] = nodeObj,
        };
        Assert.Throws<DomainInvariantException>(() =>
            AddressSelectorEvaluator.Resolve(
                AddressSelector.Create([siteObj.Id]),
                IpAddressFamily.IPv4,
                catalog,
                company));
    }

    private static AddressObject CompanyObj(string name, params AddressEntry[] entries)
        => AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create(name),
            IpAddressFamily.IPv4,
            entries);
}
