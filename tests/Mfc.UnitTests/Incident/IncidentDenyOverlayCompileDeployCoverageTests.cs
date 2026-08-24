using Mfc.Domain.Endpoint;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Incident;

public sealed class IncidentDenyOverlayCompileDeployCoverageTests
{
    [Fact]
    public void MergeWithNoOverlayLayersReturnsZeroActiveOverlayCount()
    {
        IncidentDenyOverlayCompileMerge.MergeResult result =
            IncidentDenyOverlayCompileMerge.Merge([], [], DateTimeOffset.UtcNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ActiveOverlayCount);
    }

    [Fact]
    public void BindingRequiredCodeIsStable()
    {
        Assert.Equal("INCIDENT_DENY_OVERLAY_BINDING_REQUIRED", IncidentDenyOverlayCodes.BindingRequired);
    }
}
