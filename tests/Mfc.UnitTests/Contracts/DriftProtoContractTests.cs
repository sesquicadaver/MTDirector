using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

public sealed class DriftProtoContractTests
{
    [Fact]
    public void DriftServiceExposesReadOnlyListAndGetRpcs()
    {
        string[] methods = DriftService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["GetDriftEvent", "ListDeviceDriftEvents"], methods);
        Assert.Equal("mfc.v1.DriftService", DriftService.Descriptor.FullName);
    }

    [Fact]
    public void DriftContractHasNoAutomaticRepairSurface()
    {
        foreach (string name in DriftService.Descriptor.Methods.Select(static m => m.Name)
                     .Concat(DriftService.Descriptor.File.MessageTypes.Select(static m => m.Name)))
        {
            Assert.DoesNotContain("ForceRepair", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AutoHeal", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AutoRepair", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FixAll", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DriftEventCarriesSemanticDiffAndImmutableFlag()
    {
        Assert.NotNull(DriftEvent.Descriptor.FindFieldByName("semantic_diff_canonical"));
        Assert.NotNull(DriftEvent.Descriptor.FindFieldByName("immutable"));
        Assert.NotNull(DriftEvent.Descriptor.FindFieldByName("baseline_committed_hash"));
        Assert.NotNull(DriftEvent.Descriptor.FindFieldByName("actual_managed_resource_hash"));
    }
}
