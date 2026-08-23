using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Living Spec matrix for Issue Set M7.3-06 AC (IncidentSignal ↔ ResponseAssessment contract).</summary>
public sealed class IncidentResponseAssessmentContractLivingSpecTests
{
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T10_01 = new(2026, 8, 22, 10, 0, 1, TimeSpan.Zero);

    [Fact]
    public void Ac1EventIdMapsOneToOneToIncidentId()
    {
        EventId eventId = EventId.New();
        IncidentSignal signal = SampleSignal(eventId);
        IncidentId incidentId = IncidentResponseAssessmentContract.MapIncidentId(signal);
        Assert.Equal(eventId.Value, incidentId.Value);
    }

    [Fact]
    public void Ac2OriginalFlowPreferredOverFlowForCorrelation()
    {
        FlowTuple original = Flow("10.0.0.8", "198.51.100.10");
        FlowTuple translated = Flow("10.0.0.8", "203.0.113.10");
        IncidentSignal signal = IncidentSignal.Create(
            EventId.New(),
            "evt-1",
            T10,
            T10_01,
            IncidentSignalSourceType.Ndr,
            "c2_beacon",
            IncidentSeverity.High,
            90,
            "dedup:1",
            flow: translated,
            originalFlow: original);

        FlowTuple correlationFlow = IncidentResponseAssessmentContract.ResolveCorrelationFlow(signal);
        Assert.Equal("198.51.100.10", correlationFlow.DestinationAddress);
    }

    [Fact]
    public void Ac3MissingCorrelationFlowFailsClosed()
    {
        IncidentSignal signal = IncidentSignal.Create(
            EventId.New(),
            "evt-2",
            T10,
            T10_01,
            IncidentSignalSourceType.Siem,
            "malware",
            IncidentSeverity.Medium,
            70,
            "dedup:2");

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentResponseAssessmentContract.ResolveCorrelationFlow(signal));
        Assert.Contains(IncidentResponseAssessmentCodes.MissingCorrelationFlow, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4CpuPathWithFullSessionYieldsFullyEnforceableAssessment()
    {
        IncidentResponseAssessmentBinding binding = Bind(
            SampleSignal(),
            sessionVisibility: SessionVisibilityStatus.Full,
            packetPathClass: ObservedPacketPathClass.CpuFirewall);

        Assert.Equal(ResponseAssessmentFeasibility.FullyEnforceable, binding.Assessment.Feasibility);
        Assert.Equal(AssessmentVisibilityStatus.Full, binding.Assessment.VisibilityStatus);
    }

    [Fact]
    public void Ac5HardwareOffloadedPathYieldsNotEnforceableAssessment()
    {
        IncidentResponseAssessmentBinding binding = Bind(
            SampleSignal(),
            sessionVisibility: SessionVisibilityStatus.Full,
            packetPathClass: ObservedPacketPathClass.HardwareOffloaded);

        Assert.Equal(ResponseAssessmentFeasibility.NotEnforceableByIpFilter, binding.Assessment.Feasibility);
        Assert.Equal(AssessmentVisibilityStatus.Partial, binding.Assessment.VisibilityStatus);
    }

    [Fact]
    public void Ac6PartialSessionVisibilityYieldsNewConnectionsOnly()
    {
        ResponseAssessmentFeasibility feasibility = IncidentResponseFeasibilityClassifier.Classify(
            ObservedPacketPathClass.CpuFirewall,
            SessionVisibilityStatus.Partial);

        Assert.Equal(ResponseAssessmentFeasibility.NewConnectionsOnly, feasibility);
    }

    [Fact]
    public void Ac7HighSignalConfidenceMayExceedAssessmentWhenVisibilityLimited()
    {
        IncidentSignal signal = SampleSignal(confidence: 95);
        IncidentResponseAssessmentBinding binding = Bind(
            signal,
            sessionVisibility: SessionVisibilityStatus.NotObserved,
            packetPathClass: ObservedPacketPathClass.CpuFirewall);

        Assert.True(binding.Assessment.Confidence < signal.Confidence);
        Assert.Contains(
            binding.Findings,
            f => f.Code == IncidentResponseAssessmentCodes.SignalConfidenceExceedsAssessment);
    }

    [Fact]
    public void Ac8AssessmentCarriesMappedIncidentId()
    {
        EventId eventId = EventId.New();
        IncidentResponseAssessmentBinding binding = Bind(SampleSignal(eventId));

        Assert.Equal(eventId.Value, binding.Assessment.IncidentId.Value);
        Assert.Equal(eventId.Value, binding.IncidentId.Value);
    }

    [Fact]
    public void Ac9BindingViewRoundTripsContractFields()
    {
        IncidentResponseAssessmentBinding binding = Bind(SampleSignal());
        IncidentResponseAssessmentBindingView view = IncidentResponseAssessmentBindingView.FromBinding(binding);

        Assert.Equal(binding.IncidentId.Value, view.IncidentId);
        Assert.Equal(binding.CorrelationFlow.Protocol, view.CorrelationFlow.Protocol);
        Assert.Equal(binding.Assessment.Confidence, view.Assessment.Confidence);
        Assert.Contains(view.Findings, f => f.Code == IncidentResponseAssessmentCodes.ContractBound);
    }

    [Fact]
    public async Task Ac10UseCaseReturnsViewAndRejectsUnauthorized()
    {
        FakeAuthorizationBoundary auth = new();
        BindIncidentResponseAssessmentUseCase useCase = new(auth);
        IncidentSignal signal = SampleSignal();
        EndpointId endpointId = EndpointId.New();
        PresenceId presenceId = PresenceId.New();
        NodeId nodeId = NodeId.New();

        ApplicationResult<IncidentResponseAssessmentBindingView> ok = await useCase.ExecuteAsync(
            new BindIncidentResponseAssessmentCommand
            {
                Actor = "analyst",
                Signal = signal,
                EndpointId = endpointId.Value,
                PresenceId = presenceId.Value,
                EnforcementNodeId = nodeId.Value,
                AssessedAt = T10,
                SessionVisibility = SessionVisibilityStatus.Full,
                PacketPathClass = ObservedPacketPathClass.CpuFirewall,
            });
        Assert.True(ok.IsSuccess);
        Assert.Equal(signal.EventId.Value, ok.Value!.IncidentId);

        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentAssessmentBind);
        ApplicationResult<IncidentResponseAssessmentBindingView> denied = await useCase.ExecuteAsync(
            new BindIncidentResponseAssessmentCommand
            {
                Actor = "analyst",
                Signal = signal,
                EndpointId = endpointId.Value,
                PresenceId = presenceId.Value,
                EnforcementNodeId = nodeId.Value,
                AssessedAt = T10,
            });
        Assert.True(denied.IsFailure);
        Assert.Equal("forbidden", denied.Error?.Code);
    }

    private static IncidentResponseAssessmentBinding Bind(
        IncidentSignal signal,
        SessionVisibilityStatus? sessionVisibility = null,
        ObservedPacketPathClass packetPathClass = ObservedPacketPathClass.Unknown)
        => IncidentResponseAssessmentContract.Bind(
            new IncidentResponseAssessmentQuery
            {
                Signal = signal,
                EndpointId = EndpointId.New(),
                PresenceId = PresenceId.New(),
                EnforcementNodeId = NodeId.New(),
                AssessedAt = T10,
                SessionVisibility = sessionVisibility,
                RouteTrace = CpuTrace(),
                PacketPathClass = packetPathClass,
            });

    private static IncidentSignal SampleSignal(EventId? eventId = null, int confidence = 85)
        => IncidentSignal.Create(
            eventId ?? EventId.New(),
            "evt-sample",
            T10,
            T10_01,
            IncidentSignalSourceType.Ndr,
            "lateral_movement",
            IncidentSeverity.High,
            confidence,
            "dedup:sample",
            flow: Flow("10.0.0.8", "203.0.113.10"));

    private static FlowTuple Flow(string source, string destination)
        => FlowTuple.Create(
            sourceAddress: source,
            destinationAddress: destination,
            destinationPort: 443,
            protocol: "tcp");

    private static RouteResolutionTrace CpuTrace()
        => new()
        {
            Family = "ipv4",
            DestinationAddress = "203.0.113.10",
            Decision = RouteResolutionDecisions.Forward,
            ExecutionPath = RouteResolutionExecutionPaths.Cpu,
            Certainty = RouteResolutionCertainties.Definite,
            EgressInterfaces = ["ether2"],
        };
}
