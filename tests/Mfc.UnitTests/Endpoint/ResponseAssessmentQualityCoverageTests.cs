using Mfc.Application.Endpoint;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Xunit;

namespace Mfc.UnitTests.Endpoint;

/// <summary>Extra branch coverage for M7.3-05 response assessment quality modules.</summary>
public sealed class ResponseAssessmentQualityCoverageTests
{
    [Fact]
    public void ReconstituteRejectsOutOfRangeConfidence()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            ResponseAssessment.Reconstitute(
                AssessmentId.New(),
                IncidentId.New(),
                EndpointId.New(),
                PresenceId.New(),
                NodeId.New(),
                ResponseAssessmentFeasibility.FullyEnforceable,
                AssessmentVisibilityStatus.Full,
                confidence: 101,
                ResponseAssessmentStatus.Active,
                DateTimeOffset.UtcNow));
        Assert.Contains("confidence must be between 0 and 100", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidatePreservesVisibilityAndConfidence()
    {
        DateTimeOffset created = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        ResponseAssessment active = ResponseAssessment.CreateActive(
            IncidentId.New(),
            EndpointId.New(),
            PresenceId.New(),
            NodeId.New(),
            ResponseAssessmentFeasibility.FullyEnforceable,
            created,
            qualityInput: new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.FullyEnforceable,
                SessionVisibility = SessionVisibilityStatus.Full,
                RouteTrace = new RouteResolutionTrace
                {
                    Family = "ipv4",
                    DestinationAddress = "203.0.113.10",
                    Decision = RouteResolutionDecisions.Forward,
                    ExecutionPath = RouteResolutionExecutionPaths.Cpu,
                    Certainty = RouteResolutionCertainties.Definite,
                },
            });

        ResponseAssessment invalidated = active.Invalidate(
            created.AddHours(1),
            EndpointMobilityCodes.MobilityInvalidation);

        Assert.Equal(active.VisibilityStatus, invalidated.VisibilityStatus);
        Assert.Equal(active.Confidence, invalidated.Confidence);
    }

    [Fact]
    public void EvaluatorFlagsIndeterminateRouteCertainty()
    {
        ResponseAssessmentQualityResult result = ResponseAssessmentQualityEvaluator.Evaluate(
            new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.FullyEnforceable,
                SessionVisibility = SessionVisibilityStatus.Full,
                RouteTrace = new RouteResolutionTrace
                {
                    Family = "ipv4",
                    DestinationAddress = "203.0.113.10",
                    Decision = RouteResolutionDecisions.Forward,
                    ExecutionPath = RouteResolutionExecutionPaths.Cpu,
                    Certainty = RouteResolutionCertainties.Indeterminate,
                },
            });

        Assert.Contains(result.Findings, f => f.Code == ResponseAssessmentQualityCodes.IndeterminateRouteCertainty);
    }

    [Fact]
    public void UseCaseValidatesNullInput()
    {
        EvaluateResponseAssessmentQualityUseCase useCase = new(new Mfc.UnitTests.Application.Fakes.FakeAuthorizationBoundary());
        Assert.Throws<ArgumentNullException>(() =>
            useCase.ExecuteAsync(
                new EvaluateResponseAssessmentQualityCommand
                {
                    Actor = "tester",
                    Input = null!,
                }).GetAwaiter().GetResult());
    }
}
