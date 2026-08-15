using System.Text.Json;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyObjectJsonReaderTests
{
    [Fact]
    public void ReadsHostPrefixAndRange()
    {
        Guid id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        PolicyObjectIdentity identity = new(id, PolicyObjectOwnerScope.Company, null);
        JsonElement json = JsonDocument.Parse(
            """
            {"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","name":"corp","family":"IPv4","entries":[
              {"kind":"HOST","address":"10.0.0.1"},
              {"kind":"PREFIX","address":"10.1.0.0","prefix_length":24},
              {"kind":"RANGE","start":"10.2.0.1","end":"10.2.0.5"}
            ]}
            """).RootElement.Clone();
        Assert.True(PolicyObjectJsonReader.TryReadAddress(json, identity, out AddressObject? obj, out string? error));
        Assert.Null(error);
        Assert.NotNull(obj);
        Assert.Equal(IpAddressFamily.IPv4, obj!.Family);
        Assert.True(obj.Intervals.Count >= 2);
    }

    [Fact]
    public void MissingEntriesFails()
    {
        Guid id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        JsonElement json = JsonDocument.Parse(
            "{\"id\":\"" + id + "\",\"name\":\"corp\",\"family\":\"IPv4\"}").RootElement.Clone();
        Assert.False(PolicyObjectJsonReader.TryReadAddress(
            json,
            new PolicyObjectIdentity(id, PolicyObjectOwnerScope.Company, null),
            out _,
            out string? error));
        Assert.Contains("entries", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsTcpServiceTerm()
    {
        Guid id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        JsonElement json = JsonDocument.Parse(
            """
            {"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","name":"http","terms":[
              {"protocol":{"number":6,"canonical_name":"tcp"},"destination_ports":[{"start":80,"end":80}]}
            ]}
            """).RootElement.Clone();
        Assert.True(PolicyObjectJsonReader.TryReadService(
            json,
            new PolicyObjectIdentity(id, PolicyObjectOwnerScope.Company, null),
            out ServiceObject? obj,
            out string? error));
        Assert.Null(error);
        Assert.NotNull(obj);
        Assert.Equal(IpProtocol.Tcp, obj!.Terms[0].Protocol.Number);
        Assert.Equal(new PortInterval(80, 80), obj.Terms[0].DestinationPorts!.Intervals[0]);
    }

    [Fact]
    public void MissingServicePortBoundsFailsClosed()
    {
        Guid id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        JsonElement json = JsonDocument.Parse(
            """
            {"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","name":"http","terms":[
              {"protocol":{"number":6,"canonical_name":"tcp"},"destination_ports":[{}]}
            ]}
            """).RootElement.Clone();
        Assert.False(PolicyObjectJsonReader.TryReadService(
            json,
            new PolicyObjectIdentity(id, PolicyObjectOwnerScope.Company, null),
            out _,
            out string? error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void NonArrayServicePortsFailsClosed()
    {
        Guid id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        JsonElement json = JsonDocument.Parse(
            """
            {"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","name":"http","terms":[
              {"protocol":{"number":6,"canonical_name":"tcp"},"destination_ports":{"start":80,"end":80}}
            ]}
            """).RootElement.Clone();
        Assert.False(PolicyObjectJsonReader.TryReadService(
            json,
            new PolicyObjectIdentity(id, PolicyObjectOwnerScope.Company, null),
            out _,
            out string? error));
        Assert.Contains("destination_ports", error, StringComparison.Ordinal);
    }
}
