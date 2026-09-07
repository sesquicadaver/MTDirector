using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

public sealed class RoutingAssuranceProtoContractTests
{
    [Fact]
    public void RoutingAssuranceServiceExposesOnlyGetRpc()
    {
        string[] methods = RoutingAssuranceService.Descriptor.Methods
            .Select(static m => m.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(["GetDeviceRoutingAssuranceState"], methods);
        Assert.Equal("mfc.v1.RoutingAssuranceService", RoutingAssuranceService.Descriptor.FullName);
    }

    [Fact]
    public void RoutingAssuranceContractHasNoWriteSurface()
    {
        foreach (string name in RoutingAssuranceService.Descriptor.Methods.Select(static m => m.Name)
                     .Concat(RoutingAssuranceService.Descriptor.File.MessageTypes.Select(static m => m.Name)))
        {
            Assert.DoesNotContain("Upsert", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Write", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Delete", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Mutate", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RoutingAssuranceStateDetailCarriesHashesAndBoundedRows()
    {
        Assert.NotNull(RoutingAssuranceStateDetail.Descriptor.FindFieldByName("configuration_hash"));
        Assert.NotNull(RoutingAssuranceStateDetail.Descriptor.FindFieldByName("operational_hash"));
        Assert.NotNull(RoutingAssuranceStateDetail.Descriptor.FindFieldByName("expectations"));
        Assert.NotNull(RoutingAssuranceStateDetail.Descriptor.FindFieldByName("findings"));
        Assert.NotNull(RoutingAssuranceStateDetail.Descriptor.FindFieldByName("trace_summaries"));
        Assert.NotNull(RoutingAssuranceStateDetail.Descriptor.FindFieldByName("row_version"));
    }
}
