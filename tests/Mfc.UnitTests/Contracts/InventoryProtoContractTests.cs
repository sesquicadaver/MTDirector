using Google.Protobuf;
using Google.Protobuf.Reflection;
using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

/// <summary>Contract round-trip / forward-compat checks for M1-25 inventory protos.</summary>
public sealed class InventoryProtoContractTests
{
    [Fact]
    public void UuidRoundTripsAsNetworkByteOrderGuid()
    {
        Guid original = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        byte[] bigEndian = original.ToByteArray(bigEndian: true);
        Uuid proto = new() { Value = ByteString.CopyFrom(bigEndian) };
        Uuid parsed = Uuid.Parser.ParseFrom(proto.ToByteArray());
        Assert.Equal(16, parsed.Value.Length);
        Guid restored = new(parsed.Value.Span, bigEndian: true);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void SiteAndCreateSiteRequestRoundTrip()
    {
        Guid siteId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid key = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Site site = new()
        {
            Id = new Uuid { Value = ByteString.CopyFrom(siteId.ToByteArray(bigEndian: true)) },
            Code = "EDGE01",
            Name = "Edge",
            Status = SiteStatus.Active,
            RowVersion = 3,
        };
        Site site2 = Site.Parser.ParseFrom(site.ToByteArray());
        Assert.Equal(site.Code, site2.Code);
        Assert.Equal(site.Status, site2.Status);
        Assert.Equal(site.RowVersion, site2.RowVersion);

        CreateSiteRequest request = new()
        {
            IdempotencyKey = new Uuid { Value = ByteString.CopyFrom(key.ToByteArray(bigEndian: true)) },
            Code = "EDGE01",
            Name = "Edge",
        };
        CreateSiteRequest request2 = CreateSiteRequest.Parser.ParseFrom(request.ToByteArray());
        Assert.Equal(request.Code, request2.Code);
        Assert.Equal(16, request2.IdempotencyKey.Value.Length);
    }

    [Fact]
    public void UnknownEnumValuesArePreservedOnRoundTrip()
    {
        // Proto3 preserves unknown enum numeric values for forward compatibility.
        Site site = new()
        {
            Id = new Uuid { Value = ByteString.CopyFrom(Guid.NewGuid().ToByteArray(bigEndian: true)) },
            Code = "FWD01",
            Name = "Forward",
            Status = (SiteStatus)99,
            RowVersion = 1,
        };
        Site parsed = Site.Parser.ParseFrom(site.ToByteArray());
        Assert.Equal((SiteStatus)99, parsed.Status);
    }

    [Fact]
    public void InventoryServiceDescriptorExposesVerticalSliceRpcs()
    {
        ServiceDescriptor descriptor = InventoryService.Descriptor;
        string[] methods = descriptor.Methods.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(
            [
                "CreateNode",
                "CreateSite",
                "GetNode",
                "ListNodes",
                "ListSites",
                "RegisterDevice",
                "UpdateDevice",
                "UpdateDeviceConnection",
                "ValidateDeviceConnection",
            ],
            methods);
        Assert.DoesNotContain("DiscoverDevice", methods);
        Assert.DoesNotContain("GetDiscoveryStatus", methods);
    }

    [Fact]
    public void DeviceConnectionSummaryHasNoPasswordFields()
    {
        MessageDescriptor descriptor = DeviceConnectionSummary.Descriptor;
        Assert.DoesNotContain(
            descriptor.Fields.InDeclarationOrder(),
            f => f.Name.Contains("password", StringComparison.OrdinalIgnoreCase)
                 || f.Name.Contains("secret", StringComparison.OrdinalIgnoreCase)
                 || f.Name.Contains("cipher", StringComparison.OrdinalIgnoreCase));
    }
}
