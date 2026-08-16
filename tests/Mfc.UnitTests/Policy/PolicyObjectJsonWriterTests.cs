using System.Net;
using System.Text.Json;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyObjectJsonWriterTests
{
    [Fact]
    public void AddressRoundTripsHostPrefixAndRange()
    {
        Guid id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        AddressObject original = AddressObject.Reconstitute(
            new AddressObjectId(id),
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("corp"),
            IpAddressFamily.IPv4,
            "desc",
            [
                AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")).ToInterval(),
                AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.1.0.0"), 24).ToInterval(),
                AddressEntry.Range(IpAddressFamily.IPv4, IPAddress.Parse("10.2.0.1"), IPAddress.Parse("10.2.0.5"))
                    .ToInterval(),
            ]);
        JsonElement json = PolicyObjectJsonWriter.WriteAddress(original);
        Assert.True(PolicyObjectJsonReader.TryReadAddress(
            json,
            new PolicyObjectIdentity(id, PolicyObjectOwnerScope.Company, null),
            out AddressObject? roundTrip,
            out string? error));
        Assert.Null(error);
        Assert.NotNull(roundTrip);
        Assert.Equal(original.Name.Value, roundTrip!.Name.Value);
        Assert.Equal(original.Family, roundTrip.Family);
        Assert.Equal(original.Intervals, roundTrip.Intervals);
        Assert.Equal("desc", roundTrip.Description);
    }

    [Fact]
    public void Ipv6PrefixRoundTrips()
    {
        Guid id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        AddressObject original = AddressObject.Reconstitute(
            new AddressObjectId(id),
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("v6"),
            IpAddressFamily.IPv6,
            null,
            [AddressEntry.Prefix(IpAddressFamily.IPv6, IPAddress.Parse("2001:db8::"), 64).ToInterval()]);
        JsonElement json = PolicyObjectJsonWriter.WriteAddress(original);
        Assert.Equal("PREFIX", json.GetProperty("entries")[0].GetProperty("kind").GetString());
        Assert.True(PolicyObjectJsonReader.TryReadAddress(
            json,
            new PolicyObjectIdentity(id, PolicyObjectOwnerScope.Company, null),
            out AddressObject? roundTrip,
            out _));
        Assert.Equal(original.Intervals, roundTrip!.Intervals);
    }

    [Fact]
    public void ServiceRoundTripsTcpTerm()
    {
        Guid id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        ServiceObject original = ServiceObject.Reconstitute(
            new ServiceObjectId(id),
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("http"),
            null,
            [ServiceTerm.Create(IpProtocol.Create(6, "tcp"), destinationPorts: PortSet.Create([new PortInterval(80, 80)]))]);
        JsonElement json = PolicyObjectJsonWriter.WriteService(original);
        Assert.True(PolicyObjectJsonReader.TryReadService(
            json,
            new PolicyObjectIdentity(id, PolicyObjectOwnerScope.Company, null),
            out ServiceObject? roundTrip,
            out string? error));
        Assert.Null(error);
        Assert.NotNull(roundTrip);
        Assert.Equal(original.Terms, roundTrip!.Terms);
    }

    [Fact]
    public void WithCatalogHelpersReplaceCollections()
    {
        PolicyDocument empty = PolicyDocument.CreateEmpty(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company);
        JsonElement address = JsonDocument.Parse(
            """{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","name":"a","family":"IPv4","entries":[{"kind":"HOST","address":"10.0.0.1"}]}""")
            .RootElement.Clone();
        JsonElement service = JsonDocument.Parse(
            """{"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","name":"s","terms":[{"protocol":{"number":6},"destination_ports":[{"start":443,"end":443}]}]}""")
            .RootElement.Clone();
        JsonElement test = JsonDocument.Parse("""{"id":"dddddddd-dddd-dddd-dddd-dddddddddddd"}""").RootElement.Clone();
        PolicyDocument next = empty
            .WithAddressObjects([address])
            .WithServiceObjects([service])
            .WithTests([test]);
        Assert.Single(next.AddressObjects);
        Assert.Single(next.ServiceObjects);
        Assert.Single(next.Tests);
    }
}
