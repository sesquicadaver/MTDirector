using System.Security.Cryptography;
using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyParentContextHashTests
{
    [Fact]
    public void CompanyBaselineHasNullParentContext()
    {
        Assert.Null(PolicyHashing.ComputeParentContextHash(
            PolicyKind.CompanyBaseline,
            companyBaselineHash: null,
            siteOverlayHash: null,
            nodeOverlayHash: null,
            waivedRuleHash: null));
    }

    [Fact]
    public void SiteOverlayUsesCompanyBaselineHashDirectly()
    {
        Hash256 company = Hash256.Create(SHA256.HashData("company"u8));
        Hash256? parent = PolicyHashing.ComputeParentContextHash(
            PolicyKind.SiteOverlay,
            company,
            siteOverlayHash: null,
            nodeOverlayHash: null,
            waivedRuleHash: null);
        Assert.NotNull(parent);
        Assert.Equal(company.ToString(), parent.ToString());
    }

    [Fact]
    public void NodeOverlayCompositesCompanyAndOptionalSite()
    {
        Hash256 company = Hash256.Create(SHA256.HashData("company"u8));
        Hash256 site = Hash256.Create(SHA256.HashData("site"u8));

        Hash256? withSite = PolicyHashing.ComputeParentContextHash(
            PolicyKind.NodeOverlay, company, site, null, null);
        Hash256? withoutSite = PolicyHashing.ComputeParentContextHash(
            PolicyKind.NodeOverlay, company, null, null, null);

        Assert.NotNull(withSite);
        Assert.NotNull(withoutSite);
        Assert.NotEqual(withSite.ToString(), withoutSite.ToString());
        Assert.NotEqual(company.ToString(), withSite.ToString());
    }

    [Fact]
    public void ExceptionRequiresCompanyAndWaivedRule()
    {
        Hash256 company = Hash256.Create(SHA256.HashData("company"u8));
        Hash256 waived = Hash256.Create(SHA256.HashData("rule"u8));

        Assert.Throws<DomainInvariantException>(() =>
            PolicyHashing.ComputeParentContextHash(
                PolicyKind.Exception, null, null, null, waived));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyHashing.ComputeParentContextHash(
                PolicyKind.Exception, company, null, null, null));

        Hash256? parent = PolicyHashing.ComputeParentContextHash(
            PolicyKind.Exception, company, null, null, waived);
        Assert.NotNull(parent);
    }
}
