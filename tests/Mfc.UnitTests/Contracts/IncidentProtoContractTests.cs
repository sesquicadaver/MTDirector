using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

public sealed class IncidentProtoContractTests
{
    [Fact]
    public void IncidentServiceExposesIngestAndBindRpcsOnly()
    {
        string[] methods = IncidentService.Descriptor.Methods
            .Select(static m => m.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(["BindIncidentResponseAssessment", "IngestIncidentSignal"], methods);
        Assert.Equal("mfc.v1.IncidentService", IncidentService.Descriptor.FullName);
    }

    [Fact]
    public void IncidentContractHasNoDeployOverlayOrFeedbackRpcs()
    {
        foreach (string name in IncidentService.Descriptor.Methods.Select(static m => m.Name)
                     .Concat(IncidentService.Descriptor.File.MessageTypes.Select(static m => m.Name)))
        {
            Assert.DoesNotContain("Deploy", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Overlay", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Feedback", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ForceRepair", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void IncidentSignalCarriesDeduplicationAndSeverity()
    {
        Assert.NotNull(IncidentSignal.Descriptor.FindFieldByName("deduplication_key"));
        Assert.NotNull(IncidentSignal.Descriptor.FindFieldByName("severity"));
        Assert.NotNull(IncidentSignal.Descriptor.FindFieldByName("source_type"));
        Assert.NotNull(IngestIncidentSignalRequest.Descriptor.FindFieldByName("forbidden_ingress_field_names"));
    }
}
