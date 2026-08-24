using Mfc.Application.Incident;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Extra branch coverage for M7.4-02 ResponseIntent feasibility modules.</summary>
public sealed class ResponseIntentFeasibilityCoverageTests
{
    [Fact]
    public void RestoreCommittedPolicyIsFullyEnforceable()
    {
        ResponseIntentFeasibilityResult result = ResponseIntentFeasibilityMatrix.Assess(new ResponseIntentFeasibilityQuery
        {
            Intent = Intent(ResponseIntentAction.RestoreCommittedPolicy),
        });
        Assert.Equal(ResponseAssessmentFeasibility.FullyEnforceable, result.Feasibility);
    }

    [Fact]
    public void PartialSessionVisibilityYieldsNewConnectionsOnly()
    {
        ResponseIntentFeasibilityResult result = ResponseIntentFeasibilityMatrix.Assess(new ResponseIntentFeasibilityQuery
        {
            Intent = Intent(),
            PacketPathClass = ObservedPacketPathClass.CpuFirewall,
            SessionVisibility = SessionVisibilityStatus.Partial,
        });
        Assert.Equal(ResponseAssessmentFeasibility.NewConnectionsOnly, result.Feasibility);
    }

    [Fact]
    public void HardwareRouteTraceYieldsNotEnforceable()
    {
        ResponseIntentFeasibilityResult result = ResponseIntentFeasibilityMatrix.Assess(new ResponseIntentFeasibilityQuery
        {
            Intent = Intent(),
            PacketPathClass = ObservedPacketPathClass.CpuFirewall,
            RouteTrace = new RouteResolutionTrace
            {
                Family = "ipv4",
                DestinationAddress = "203.0.113.10",
                Decision = RouteResolutionDecisions.Forward,
                ExecutionPath = RouteResolutionExecutionPaths.Hardware,
            },
        });
        Assert.Equal(ResponseAssessmentFeasibility.NotEnforceableByIpFilter, result.Feasibility);
    }

    [Fact]
    public async Task UseCaseValidatesNullQuery()
    {
        FakeAuthorizationBoundary auth = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new();
        AssessResponseIntentFeasibilityUseCase useCase =
            new(auth, ResponseFeedbackTestFactory.CreateEmit(auth, new FakeResponseFeedbackEventStore(), audit, clock));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            useCase.ExecuteAsync(new AssessResponseIntentFeasibilityCommand
            {
                Actor = "tester",
                Query = null!,
            }));
    }

    private static ResponseIntent Intent(ResponseIntentAction action = ResponseIntentAction.TemporaryPreStateDeny)
        => ResponseIntent.Create(
            new IncidentId(Guid.NewGuid()),
            new NodeId(Guid.NewGuid()),
            action,
            TrafficPredicate.Create(),
            DateTimeOffset.UtcNow.AddHours(1),
            ResponseIntentUrgency.Emergency,
            ["evt:1"],
            "analyst",
            Guid.NewGuid());
}
