using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Endpoint;
using Mfc.Application.Models;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Endpoint;

/// <summary>Living Spec matrix for Issue Set M7.3-05 AC (assessment visibility/confidence).</summary>
public sealed class ResponseAssessmentQualityLivingSpecTests
{
    [Fact]
    public void Ac1FullyEnforceableWithFullObservationYieldsHighConfidence()
    {
        ResponseAssessmentQualityResult result = ResponseAssessmentQualityEvaluator.Evaluate(
            new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.FullyEnforceable,
                SessionVisibility = SessionVisibilityStatus.Full,
                RouteTrace = CpuTrace(),
                PacketPathClass = ObservedPacketPathClass.CpuFirewall,
            });

        Assert.Equal(AssessmentVisibilityStatus.Full, result.VisibilityStatus);
        Assert.True(result.Confidence >= 90);
    }

    [Fact]
    public void Ac2HardwareOffloadedRouteTraceLimitsVisibility()
    {
        ResponseAssessmentQualityResult result = ResponseAssessmentQualityEvaluator.Evaluate(
            new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.FullyEnforceable,
                SessionVisibility = SessionVisibilityStatus.Full,
                RouteTrace = CpuTrace(RouteResolutionExecutionPaths.Hardware),
            });

        Assert.Equal(AssessmentVisibilityStatus.Partial, result.VisibilityStatus);
        Assert.Contains(result.Findings, f => f.Code == ResponseAssessmentQualityCodes.HardwareOffloadLimitedVisibility);
    }

    [Fact]
    public void Ac3SessionNotObservedFailsClosedToNotObserved()
    {
        ResponseAssessmentQualityResult result = ResponseAssessmentQualityEvaluator.Evaluate(
            new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.FullyEnforceable,
                SessionVisibility = SessionVisibilityStatus.NotObserved,
                RouteTrace = CpuTrace(),
            });

        Assert.Equal(AssessmentVisibilityStatus.NotObserved, result.VisibilityStatus);
        Assert.Contains(result.Findings, f => f.Code == ResponseAssessmentQualityCodes.SessionNotObserved);
    }

    [Fact]
    public void Ac4IndeterminateFeasibilityReducesConfidence()
    {
        ResponseAssessmentQualityResult result = ResponseAssessmentQualityEvaluator.Evaluate(
            new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.Indeterminate,
                SessionVisibility = SessionVisibilityStatus.Full,
                RouteTrace = CpuTrace(),
            });

        Assert.Equal(AssessmentVisibilityStatus.Partial, result.VisibilityStatus);
        Assert.True(result.Confidence <= 40);
        Assert.Contains(result.Findings, f => f.Code == ResponseAssessmentQualityCodes.IndeterminateFeasibility);
    }

    [Fact]
    public void Ac5PartialSessionVisibilityDowngradesAssessment()
    {
        ResponseAssessmentQualityResult result = ResponseAssessmentQualityEvaluator.Evaluate(
            new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.NewConnectionsOnly,
                SessionVisibility = SessionVisibilityStatus.Partial,
                RouteTrace = CpuTrace(),
            });

        Assert.Equal(AssessmentVisibilityStatus.Partial, result.VisibilityStatus);
        Assert.Contains(result.Findings, f => f.Code == ResponseAssessmentQualityCodes.LimitedSessionVisibility);
    }

    [Fact]
    public void Ac6HardwareOffloadedPacketPathDowngradesVisibility()
    {
        ResponseAssessmentQualityResult result = ResponseAssessmentQualityEvaluator.Evaluate(
            new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.NotEnforceableByIpFilter,
                SessionVisibility = SessionVisibilityStatus.Full,
                RouteTrace = CpuTrace(),
                PacketPathClass = ObservedPacketPathClass.HardwareOffloaded,
            });

        Assert.Equal(AssessmentVisibilityStatus.Partial, result.VisibilityStatus);
        Assert.Contains(result.Findings, f => f.Code == ResponseAssessmentQualityCodes.HardwareOffloadedPacketPath);
    }

    [Fact]
    public void Ac7MixedPacketPathDowngradesVisibility()
    {
        ResponseAssessmentQualityResult result = ResponseAssessmentQualityEvaluator.Evaluate(
            new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.FullyEnforceable,
                SessionVisibility = SessionVisibilityStatus.Full,
                RouteTrace = CpuTrace(),
                PacketPathClass = ObservedPacketPathClass.Mixed,
            });

        Assert.Equal(AssessmentVisibilityStatus.Partial, result.VisibilityStatus);
        Assert.Contains(result.Findings, f => f.Code == ResponseAssessmentQualityCodes.MixedPacketPath);
    }

    [Fact]
    public void Ac8CreateActiveEmbedsEvaluatedQuality()
    {
        ResponseAssessment assessment = ResponseAssessment.CreateActive(
            IncidentId.New(),
            EndpointId.New(),
            PresenceId.New(),
            NodeId.New(),
            ResponseAssessmentFeasibility.FullyEnforceable,
            DateTimeOffset.UtcNow,
            qualityInput: new ResponseAssessmentQualityInput
            {
                Feasibility = ResponseAssessmentFeasibility.FullyEnforceable,
                SessionVisibility = SessionVisibilityStatus.Full,
                RouteTrace = CpuTrace(),
            });

        Assert.Equal(AssessmentVisibilityStatus.Full, assessment.VisibilityStatus);
        Assert.True(assessment.Confidence >= 90);
    }

    [Fact]
    public void Ac9AssessmentViewEmitsVisibilityAndConfidence()
    {
        ResponseAssessment assessment = ResponseAssessment.CreateActive(
            IncidentId.New(),
            EndpointId.New(),
            PresenceId.New(),
            NodeId.New(),
            ResponseAssessmentFeasibility.Indeterminate,
            DateTimeOffset.UtcNow);

        ResponseAssessmentView view = ResponseAssessmentView.FromDomain(assessment);

        Assert.Equal(assessment.VisibilityStatus.ToString(), view.VisibilityStatus);
        Assert.Equal(assessment.Confidence, view.Confidence);
    }

    [Fact]
    public async Task Ac10UseCaseReturnsViewAndRejectsUnauthorized()
    {
        FakeAuthorizationBoundary auth = new();
        EvaluateResponseAssessmentQualityUseCase useCase = new(auth);

        ApplicationResult<ResponseAssessmentQualityResultView> ok = await useCase.ExecuteAsync(
            new EvaluateResponseAssessmentQualityCommand
            {
                Actor = "analyst",
                Input = new ResponseAssessmentQualityInput
                {
                    Feasibility = ResponseAssessmentFeasibility.FullyEnforceable,
                    SessionVisibility = SessionVisibilityStatus.Full,
                    RouteTrace = CpuTrace(),
                },
            });
        Assert.True(ok.IsSuccess);
        Assert.Equal("Full", ok.Value!.VisibilityStatus);

        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentAssessmentRead);
        ApplicationResult<ResponseAssessmentQualityResultView> denied = await useCase.ExecuteAsync(
            new EvaluateResponseAssessmentQualityCommand
            {
                Actor = "analyst",
                Input = new ResponseAssessmentQualityInput
                {
                    Feasibility = ResponseAssessmentFeasibility.FullyEnforceable,
                    RouteTrace = CpuTrace(),
                },
            });
        Assert.True(denied.IsFailure);
        Assert.Equal("forbidden", denied.Error?.Code);
    }

    private static RouteResolutionTrace CpuTrace(string executionPath = RouteResolutionExecutionPaths.Cpu)
        => new()
        {
            Family = "ipv4",
            DestinationAddress = "203.0.113.10",
            Decision = RouteResolutionDecisions.Forward,
            ExecutionPath = executionPath,
            Certainty = RouteResolutionCertainties.Definite,
            EgressInterfaces = ["ether2"],
        };
}
