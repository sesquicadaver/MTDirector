using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Incident;

public sealed class IncidentDenyOverlayRemovalCoverageTests
{
    [Fact]
    public void RemovalPlanRequiredCodeIsStable()
    {
        Assert.Equal("INCIDENT_DENY_OVERLAY_REMOVAL_PLAN_REQUIRED", IncidentDenyOverlayCodes.RemovalPlanRequired);
    }

    [Fact]
    public void BindingNotIncidentOverlayCodeIsStable()
    {
        Assert.Equal("POLICY_BINDING_NOT_INCIDENT_OVERLAY", PolicyApprovalCodes.BindingNotIncidentOverlay);
    }
}
