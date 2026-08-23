using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Living Spec matrix for Issue Set M7.4-02 AC (ResponseIntent → ResponseAssessment feasibility).</summary>
public sealed class ResponseIntentFeasibilityLivingSpecTests
{
    private static readonly DateTimeOffset Expiry =
        new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ac1TemporaryDenyRequiresFiniteExpiresAt()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            ResponseIntent.Create(
                new IncidentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                new NodeId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                ResponseIntentAction.TemporaryPreStateDeny,
                TrafficPredicate.Create(),
                expiresAt: null,
                ResponseIntentUrgency.Normal,
                ["evt:response:1"],
                "analyst@example.com",
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")));
        Assert.Contains(ResponseIntentCodes.TemporaryDenyRequiresExpiry, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2CpuFirewallPathYieldsFullyEnforceable()
    {
        ResponseIntentFeasibilityResult result = Assess(
            packetPathClass: ObservedPacketPathClass.CpuFirewall,
            sessionVisibility: SessionVisibilityStatus.Full);
        Assert.Equal(ResponseAssessmentFeasibility.FullyEnforceable, result.Feasibility);
    }

    [Fact]
    public void Ac3HardwareOffloadedPathYieldsNotEnforceable()
    {
        ResponseIntentFeasibilityResult result = Assess(
            packetPathClass: ObservedPacketPathClass.HardwareOffloaded,
            sessionVisibility: SessionVisibilityStatus.Full);
        Assert.Equal(ResponseAssessmentFeasibility.NotEnforceableByIpFilter, result.Feasibility);
    }

    [Fact]
    public void Ac4L2BridgeVlanBypassYieldsNotEnforceable()
    {
        ResponseIntentFeasibilityResult result = Assess(l2BridgeVlanBypass: true);
        Assert.Equal(ResponseAssessmentFeasibility.NotEnforceableByIpFilter, result.Feasibility);
        Assert.Contains(result.Findings, f => f.Code == ResponseIntentCodes.L2BridgeVlanNotEnforceable);
    }

    [Fact]
    public void Ac5FastTrackSessionYieldsNewConnectionsOnly()
    {
        ResponseIntentFeasibilityResult result = Assess(fastTrackSessionActive: true);
        Assert.Equal(ResponseAssessmentFeasibility.NewConnectionsOnly, result.Feasibility);
        Assert.Contains(result.Findings, f => f.Code == ResponseIntentCodes.FastTrackLimitsToNewConnections);
    }

    [Fact]
    public void Ac6UnknownPacketPathYieldsIndeterminate()
    {
        ResponseIntentFeasibilityResult result = Assess(
            packetPathClass: ObservedPacketPathClass.Unknown,
            sessionVisibility: SessionVisibilityStatus.NotObserved);
        Assert.Equal(ResponseAssessmentFeasibility.Indeterminate, result.Feasibility);
    }

    [Fact]
    public void Ac7ProvenContainerForwardYieldsFullyEnforceable()
    {
        ResponseIntentFeasibilityResult result = Assess(
            packetPathClass: ObservedPacketPathClass.CpuFirewall,
            provenRoutedContainerForward: true);
        Assert.Equal(ResponseAssessmentFeasibility.FullyEnforceable, result.Feasibility);
        Assert.Contains(result.Findings, f => f.Code == ResponseIntentCodes.ContainerForwardProven);
    }

    [Fact]
    public void Ac8RevokeTemporaryExceptionIsFullyEnforceable()
    {
        ResponseIntentFeasibilityResult result = ResponseIntentFeasibilityMatrix.Assess(new ResponseIntentFeasibilityQuery
        {
            Intent = SampleIntent(action: ResponseIntentAction.RevokeTemporaryException),
        });
        Assert.Equal(ResponseAssessmentFeasibility.FullyEnforceable, result.Feasibility);
        Assert.Contains(result.Findings, f => f.Code == ResponseIntentCodes.NonDenyActionFullyEnforceable);
    }

    [Fact]
    public void Ac9ViewRoundTripsIntentAndFeasibility()
    {
        ResponseIntent intent = SampleIntent();
        ResponseIntentFeasibilityResult result = Assess(
            packetPathClass: ObservedPacketPathClass.CpuFirewall,
            sessionVisibility: SessionVisibilityStatus.Full);
        ResponseIntentFeasibilityView view = ResponseIntentFeasibilityView.FromResult(intent, result);
        Assert.Equal(intent.IncidentId.Value, view.IncidentId);
        Assert.Equal(intent.NodeId.Value, view.NodeId);
        Assert.Equal(ResponseIntentAction.TemporaryPreStateDeny, view.Action);
        Assert.Equal(ResponseAssessmentFeasibility.FullyEnforceable, view.Feasibility);
    }

    [Fact]
    public async Task Ac10UseCaseReturnsViewAndRejectsUnauthorized()
    {
        FakeAuthorizationBoundary auth = new();
        AssessResponseIntentFeasibilityUseCase useCase = new(auth);
        ApplicationResult<ResponseIntentFeasibilityView> ok = await useCase.ExecuteAsync(
            new AssessResponseIntentFeasibilityCommand
            {
                Actor = "tester",
                Query = new ResponseIntentFeasibilityQuery
                {
                    Intent = SampleIntent(),
                    PacketPathClass = ObservedPacketPathClass.CpuFirewall,
                    SessionVisibility = SessionVisibilityStatus.Full,
                },
            });
        Assert.True(ok.IsSuccess);
        Assert.Equal(ResponseAssessmentFeasibility.FullyEnforceable, ok.Value!.Feasibility);

        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentResponseAssess);
        ApplicationResult<ResponseIntentFeasibilityView> denied = await useCase.ExecuteAsync(
            new AssessResponseIntentFeasibilityCommand
            {
                Actor = "tester",
                Query = new ResponseIntentFeasibilityQuery { Intent = SampleIntent() },
            });
        Assert.False(denied.IsSuccess);
    }

    private static ResponseIntentFeasibilityResult Assess(
        ObservedPacketPathClass packetPathClass = ObservedPacketPathClass.Unknown,
        SessionVisibilityStatus? sessionVisibility = null,
        bool l2BridgeVlanBypass = false,
        bool provenRoutedContainerForward = false,
        bool fastTrackSessionActive = false)
        => ResponseIntentFeasibilityMatrix.Assess(new ResponseIntentFeasibilityQuery
        {
            Intent = SampleIntent(),
            PacketPathClass = packetPathClass,
            SessionVisibility = sessionVisibility,
            L2BridgeVlanBypass = l2BridgeVlanBypass,
            ProvenRoutedContainerForward = provenRoutedContainerForward,
            FastTrackSessionActive = fastTrackSessionActive,
        });

    private static ResponseIntent SampleIntent(
        ResponseIntentAction action = ResponseIntentAction.TemporaryPreStateDeny)
        => ResponseIntent.Create(
            new IncidentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new NodeId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            action,
            TrafficPredicate.Create(),
            Expiry,
            ResponseIntentUrgency.Normal,
            ["evt:response:1"],
            "analyst@example.com",
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
}
