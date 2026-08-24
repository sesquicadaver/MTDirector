using Mfc.Application.Incident;
using Mfc.Domain.Deployment;
using Mfc.Domain.Incident;
using Xunit;

namespace Mfc.UnitTests.E2E;

public sealed class IncidentResponseE2ECoverageTests
{
    [Fact]
    public void ReportDeploymentOutcomeOperationIsStable()
    {
        Assert.Equal("incident.response.report_deployment_outcome", ReportIncidentDeploymentOutcomeUseCase.Operation);
    }
}
