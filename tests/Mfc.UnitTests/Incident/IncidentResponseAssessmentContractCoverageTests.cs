using Mfc.Application.Incident;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Extra branch coverage for M7.3-06 incident ↔ assessment contract modules.</summary>
public sealed class IncidentResponseAssessmentContractCoverageTests
{
    [Fact]
    public void ClassifierReturnsIndeterminateForMixedPacketPath()
    {
        ResponseAssessmentFeasibility feasibility = IncidentResponseFeasibilityClassifier.Classify(
            ObservedPacketPathClass.Mixed,
            SessionVisibilityStatus.Full);
        Assert.Equal(ResponseAssessmentFeasibility.Indeterminate, feasibility);
    }

    [Fact]
    public void ClassifierUsesRouteTraceHardwareExecutionPath()
    {
        ResponseAssessmentFeasibility feasibility = IncidentResponseFeasibilityClassifier.Classify(
            ObservedPacketPathClass.CpuFirewall,
            SessionVisibilityStatus.Full,
            new RouteResolutionTrace
            {
                Family = "ipv4",
                DestinationAddress = "203.0.113.10",
                Decision = RouteResolutionDecisions.Forward,
                ExecutionPath = RouteResolutionExecutionPaths.Hardware,
            });
        Assert.Equal(ResponseAssessmentFeasibility.NotEnforceableByIpFilter, feasibility);
    }

    [Fact]
    public void BindAcceptsFlowWhenOriginalFlowMissing()
    {
        IncidentSignal signal = IncidentSignal.Create(
            EventId.New(),
            "evt-flow-only",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddSeconds(1),
            IncidentSignalSourceType.Ids,
            "signature_match",
            IncidentSeverity.Medium,
            60,
            "dedup:flow",
            flow: FlowTuple.Create(
                sourceAddress: "10.0.0.5",
                destinationAddress: "203.0.113.10",
                protocol: "udp"));

        IncidentResponseAssessmentBinding binding = IncidentResponseAssessmentContract.Bind(
            new IncidentResponseAssessmentQuery
            {
                Signal = signal,
                EndpointId = EndpointId.New(),
                PresenceId = PresenceId.New(),
                EnforcementNodeId = NodeId.New(),
                AssessedAt = DateTimeOffset.UtcNow,
            });

        Assert.Equal("203.0.113.10", binding.CorrelationFlow.DestinationAddress);
    }

    [Fact]
    public void UseCaseValidatesNullSignal()
    {
        BindIncidentResponseAssessmentUseCase useCase = new(new Mfc.UnitTests.Application.Fakes.FakeAuthorizationBoundary());
        Assert.Throws<ArgumentNullException>(() =>
            useCase.ExecuteAsync(
                new BindIncidentResponseAssessmentCommand
                {
                    Actor = "tester",
                    Signal = null!,
                    EndpointId = Guid.NewGuid(),
                    PresenceId = Guid.NewGuid(),
                    EnforcementNodeId = Guid.NewGuid(),
                    AssessedAt = DateTimeOffset.UtcNow,
                }).GetAwaiter().GetResult());
    }
}
