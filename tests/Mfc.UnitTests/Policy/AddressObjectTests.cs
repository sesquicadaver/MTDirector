using System.Net;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class AddressObjectTests
{
    [Fact]
    public void SupportsHostPrefixAndIpv4RangeMasksPrefixHostBits()
    {
        AddressObject obj = AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            ownerId: null,
            exceptionRevisionId: null,
            NonEmptyName.Create("corp"),
            IpAddressFamily.IPv4,
            [
                AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")),
                AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.1.2.3"), 24),
                AddressEntry.Range(IpAddressFamily.IPv4, IPAddress.Parse("10.2.0.5"), IPAddress.Parse("10.2.0.10")),
            ]);

        Assert.Contains(
            obj.Intervals,
            i => i.Start == AddressInterval.ToNumeric(IPAddress.Parse("10.1.2.0"), IpAddressFamily.IPv4)
                 && i.End == AddressInterval.ToNumeric(IPAddress.Parse("10.1.2.255"), IpAddressFamily.IPv4));
    }

    [Fact]
    public void Ipv6RangeAndFqdnStyleEntriesAreImpossible()
    {
        Assert.Throws<DomainInvariantException>(() =>
            AddressEntry.Range(
                IpAddressFamily.IPv6,
                IPAddress.Parse("2001:db8::1"),
                IPAddress.Parse("2001:db8::2")));
    }

    [Fact]
    public void FamilyMismatchIsRejected()
    {
        Assert.Throws<DomainInvariantException>(() =>
            AddressObject.Create(
                PolicyObjectOwnerScope.Company,
                null,
                null,
                NonEmptyName.Create("bad"),
                IpAddressFamily.IPv4,
                [AddressEntry.Host(IpAddressFamily.IPv6, IPAddress.Parse("2001:db8::1"))]));
    }

    [Fact]
    public void NormalizationMergesOverlapsDuplicatesAndAdjacentDeterministically()
    {
        AddressObject first = AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("a"),
            IpAddressFamily.IPv4,
            [
                AddressEntry.Range(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.5")),
                AddressEntry.Range(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.3"), IPAddress.Parse("10.0.0.8")),
                AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.9")),
                AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")),
            ]);
        AddressObject second = AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("b"),
            IpAddressFamily.IPv4,
            [
                AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")),
                AddressEntry.Range(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.3"), IPAddress.Parse("10.0.0.8")),
                AddressEntry.Range(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.5")),
                AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.9")),
            ]);

        Assert.Equal(first.Intervals, second.Intervals);
        Assert.Single(first.Intervals);
        Assert.Equal(
            AddressInterval.ToNumeric(IPAddress.Parse("10.0.0.1"), IpAddressFamily.IPv4),
            first.Intervals[0].Start);
        Assert.Equal(
            AddressInterval.ToNumeric(IPAddress.Parse("10.0.0.9"), IpAddressFamily.IPv4),
            first.Intervals[0].End);
    }

    [Fact]
    public void EmptyEntriesAreRejected()
    {
        Assert.Throws<DomainInvariantException>(() =>
            AddressObject.Create(
                PolicyObjectOwnerScope.Company,
                null,
                null,
                NonEmptyName.Create("empty"),
                IpAddressFamily.IPv4,
                []));
    }
}
