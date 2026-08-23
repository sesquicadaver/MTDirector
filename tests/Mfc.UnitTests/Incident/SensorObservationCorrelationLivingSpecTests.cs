using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Living Spec matrix for Issue Set M7.3-04 AC (sensor observation ↔ route trace correlation).</summary>
public sealed class SensorObservationCorrelationLivingSpecTests
{
    [Fact]
    public void Ac1PreroutingAlignedWhenFlowAndIngressMatchTrace()
    {
        FlowTuple flow = Flow("10.0.0.8", "203.0.113.10");
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.Prerouting,
                OriginalFlow = flow,
                IngressInterface = "ether1",
                RouteTrace = Trace(
                    destination: "203.0.113.10",
                    ingress: "ether1",
                    table: "main",
                    egress: "ether2"),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Aligned, result.Status);
        Assert.Contains(result.Findings, f => f.Code == SensorObservationCorrelationCodes.CorrelationAligned);
    }

    [Fact]
    public void Ac2MissingRouteTraceReturnsIndeterminate()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.Prerouting,
                OriginalFlow = Flow("10.0.0.8", "203.0.113.10"),
                RouteTrace = null,
            });

        Assert.Equal(SensorObservationCorrelationStatus.Indeterminate, result.Status);
        Assert.Contains(result.Findings, f => f.Code == SensorObservationCorrelationCodes.NoRouteTrace);
    }

    [Fact]
    public void Ac3HardwareOffloadMarksSensorBypassed()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.Prerouting,
                OriginalFlow = Flow("10.0.0.8", "203.0.113.10"),
                RouteTrace = Trace(
                    destination: "203.0.113.10",
                    ingress: "ether1",
                    table: "main",
                    egress: "ether2",
                    executionPath: RouteResolutionExecutionPaths.Hardware),
            });

        Assert.Equal(SensorObservationCorrelationStatus.SensorBypassed, result.Status);
        Assert.Contains(result.Findings, f => f.Code == SensorObservationCorrelationCodes.SensorBypassHwOffload);
    }

    [Fact]
    public void Ac4PostDstNatAlignedWhenTranslatedDestinationMatchesTrace()
    {
        FlowTuple original = Flow("10.0.0.8", "198.51.100.10");
        FlowTuple translated = Flow("10.0.0.8", "203.0.113.10");

        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.PostDstNat,
                OriginalFlow = original,
                TranslatedFlow = translated,
                RouteTrace = Trace(
                    destination: "203.0.113.10",
                    ingress: "ether1",
                    table: "main",
                    egress: "ether2"),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Aligned, result.Status);
    }

    [Fact]
    public void Ac5PostDstNatWithoutTranslatedFlowIsIndeterminate()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.PostDstNat,
                OriginalFlow = Flow("10.0.0.8", "198.51.100.10"),
                RouteTrace = Trace(
                    destination: "203.0.113.10",
                    ingress: "ether1",
                    table: "main",
                    egress: "ether2"),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Indeterminate, result.Status);
        Assert.Contains(result.Findings, f => f.Code == SensorObservationCorrelationCodes.MissingTranslatedFlow);
    }

    [Fact]
    public void Ac6EgressMismatchDetectsAlternateWanPath()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.Egress,
                OriginalFlow = Flow("10.0.0.8", "203.0.113.10"),
                EgressInterface = "ether1",
                RouteTrace = Trace(
                    destination: "203.0.113.10",
                    ingress: "bridge1",
                    table: "main",
                    egress: "ether2"),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Mismatched, result.Status);
        Assert.Contains(result.Findings, f => f.Code == SensorObservationCorrelationCodes.EgressInterfaceMismatch);
    }

    [Fact]
    public void Ac7VrfMismatchAtPostRoutingReturnsMismatched()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.PostRouting,
                OriginalFlow = Flow("10.0.0.8", "10.20.0.50"),
                Vrf = "internet",
                SelectedTable = "main",
                RouteTrace = Trace(
                    destination: "10.20.0.50",
                    ingress: "ether1",
                    table: "corp",
                    egress: "ipsec1",
                    vrf: "corp"),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Mismatched, result.Status);
        Assert.Contains(result.Findings, f => f.Code == SensorObservationCorrelationCodes.VrfMismatch);
    }

    [Fact]
    public void Ac8RoutingMarkMismatchAtPreroutingReturnsMismatched()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.Prerouting,
                OriginalFlow = Flow("10.0.0.8", "203.0.113.10"),
                RoutingMark = "wan2-mark",
                RouteTrace = Trace(
                    destination: "203.0.113.10",
                    ingress: "ether1",
                    table: "main",
                    egress: "ether1",
                    routingMark: "main-mark"),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Mismatched, result.Status);
        Assert.Contains(result.Findings, f => f.Code == SensorObservationCorrelationCodes.RoutingMarkMismatch);
    }

    [Fact]
    public void Ac9PostRoutingAlignedWhenTableAndDestinationMatchTrace()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.PostRouting,
                OriginalFlow = Flow("10.0.0.8", "10.20.0.50"),
                SelectedTable = "corp",
                Vrf = "corp",
                RouteTrace = Trace(
                    destination: "10.20.0.50",
                    ingress: "ether1",
                    table: "corp",
                    egress: "ipsec1",
                    vrf: "corp"),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Aligned, result.Status);
    }

    [Fact]
    public async Task Ac10UseCaseReturnsViewAndRejectsUnauthorized()
    {
        FakeAuthorizationBoundary auth = new();
        CorrelateSensorObservationUseCase useCase = new(auth);

        ApplicationResult<SensorObservationCorrelationResultView> ok = await useCase.ExecuteAsync(
            new CorrelateSensorObservationCommand
            {
                Actor = "analyst",
                Query = new SensorObservationCorrelationQuery
                {
                    ObservationPoint = SensorObservationPoint.Prerouting,
                    OriginalFlow = Flow("10.0.0.8", "203.0.113.10"),
                    RouteTrace = Trace(
                        destination: "203.0.113.10",
                        ingress: "ether1",
                        table: "main",
                        egress: "ether2"),
                },
            });
        Assert.True(ok.IsSuccess);
        Assert.Equal("Aligned", ok.Value!.Status);

        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentCorrelationRead);
        ApplicationResult<SensorObservationCorrelationResultView> denied = await useCase.ExecuteAsync(
            new CorrelateSensorObservationCommand
            {
                Actor = "analyst",
                Query = new SensorObservationCorrelationQuery
                {
                    ObservationPoint = SensorObservationPoint.Prerouting,
                    OriginalFlow = Flow("10.0.0.8", "203.0.113.10"),
                    RouteTrace = Trace(
                        destination: "203.0.113.10",
                        ingress: "ether1",
                        table: "main",
                        egress: "ether2"),
                },
            });
        Assert.True(denied.IsFailure);
        Assert.Equal("forbidden", denied.Error?.Code);
    }

    private static FlowTuple Flow(string source, string destination)
        => FlowTuple.Create(sourceAddress: source, destinationAddress: destination, protocol: "tcp");

    private static RouteResolutionTrace Trace(
        string destination,
        string ingress,
        string table,
        string egress,
        string? vrf = null,
        string? routingMark = null,
        string? executionPath = null)
        => new()
        {
            Family = "ipv4",
            SourceAddress = "10.0.0.8",
            DestinationAddress = destination,
            IngressInterface = ingress,
            InitialVrf = vrf,
            SelectedVrf = vrf,
            SelectedTable = table,
            RoutingMark = routingMark,
            Decision = RouteResolutionDecisions.Forward,
            EgressInterfaces = [egress],
            ExecutionPath = executionPath ?? RouteResolutionExecutionPaths.Cpu,
            Certainty = RouteResolutionCertainties.Definite,
        };
}
