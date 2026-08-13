using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Xunit;
using ProtoZoneDefinition = Mfc.Contracts.Mfc.V1.ZoneDefinition;

namespace Mfc.UnitTests.Contracts;

public sealed class ZoneProtoContractTests
{
    [Fact]
    public void ZoneServiceDescriptorExposesCrudAndResolveRpcs()
    {
        string[] methods = ZoneService.Descriptor.Methods.Select(m => m.Name).OrderBy(n => n).ToArray();
        Assert.Contains("CreateZoneDefinition", methods);
        Assert.Contains("UpdateZoneDefinition", methods);
        Assert.Contains("ListZoneDefinitions", methods);
        Assert.Contains("DeleteZoneDefinition", methods);
        Assert.Contains("UpsertNodeZoneBinding", methods);
        Assert.Contains("DeleteNodeZoneBinding", methods);
        Assert.Contains("ListNodeZoneBindings", methods);
        Assert.Contains("ResolveZonesForDevice", methods);
        Assert.Contains("ResolveZonesForNode", methods);
        Assert.Equal("mfc.v1.ZoneService", ZoneService.Descriptor.FullName);
    }

    [Fact]
    public void ZoneDefinitionRoundTripsOptionalOwnerAndDescription()
    {
        ProtoZoneDefinition original = new()
        {
            Id = new Uuid { Value = ByteString.CopyFrom(Guid.NewGuid().ToByteArray(bigEndian: true)) },
            OwnerScope = PolicyOwnerScope.Site,
            OwnerId = new Uuid { Value = ByteString.CopyFrom(Guid.NewGuid().ToByteArray(bigEndian: true)) },
            Key = "lan",
            Name = "LAN",
            Description = "corp lan",
            RowVersion = 3,
        };
        ProtoZoneDefinition clone = ProtoZoneDefinition.Parser.ParseFrom(original.ToByteArray());
        Assert.Equal(original.Key, clone.Key);
        Assert.Equal(original.Description, clone.Description);
        Assert.Equal(original.RowVersion, clone.RowVersion);
        Assert.Equal(original.OwnerScope, clone.OwnerScope);
    }
}
