using Mfc.Application.Incident;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Mfc.Domain.Routing;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Extra branch coverage for M7.3-04 sensor observation correlation modules.</summary>
public sealed class SensorObservationCorrelationCoverageTests
{
    [Fact]
    public void ResolverRejectsMissingOriginalDestination()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            SensorObservationCorrelationResolver.Correlate(
                new SensorObservationCorrelationQuery
                {
                    ObservationPoint = SensorObservationPoint.Prerouting,
                    OriginalFlow = FlowTuple.Create(sourceAddress: "10.0.0.1", protocol: "tcp"),
                    RouteTrace = MinimalTrace(),
                }));
        Assert.Contains(SensorObservationCorrelationCodes.MissingOriginalFlow, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolverTreatsDestinationComparisonCaseInsensitively()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.Prerouting,
                OriginalFlow = FlowTuple.Create(
                    sourceAddress: "10.0.0.1",
                    destinationAddress: "203.0.113.10",
                    protocol: "tcp"),
                RouteTrace = MinimalTrace(destination: "203.0.113.10".ToUpperInvariant()),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Aligned, result.Status);
    }

    [Fact]
    public void ResolverFlagsFlowDestinationMismatchAtPrerouting()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.Prerouting,
                OriginalFlow = FlowTuple.Create(
                    sourceAddress: "10.0.0.1",
                    destinationAddress: "203.0.113.10",
                    protocol: "tcp"),
                RouteTrace = MinimalTrace(destination: "198.51.100.4"),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Mismatched, result.Status);
        Assert.Contains(result.Findings, f => f.Code == SensorObservationCorrelationCodes.FlowDestinationMismatch);
    }

    [Fact]
    public void ResolverFlagsRoutingTableMismatchAtPostRouting()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.PostRouting,
                OriginalFlow = FlowTuple.Create(
                    sourceAddress: "10.0.0.1",
                    destinationAddress: "10.20.0.5",
                    protocol: "tcp"),
                SelectedTable = "main",
                RouteTrace = MinimalTrace(destination: "10.20.0.5", table: "corp"),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Mismatched, result.Status);
        Assert.Contains(result.Findings, f => f.Code == SensorObservationCorrelationCodes.RoutingTableMismatch);
    }

    [Fact]
    public void ResolverRequiresEgressInterfaceAtEgressObservationPoint()
    {
        SensorObservationCorrelationResult result = SensorObservationCorrelationResolver.Correlate(
            new SensorObservationCorrelationQuery
            {
                ObservationPoint = SensorObservationPoint.Egress,
                OriginalFlow = FlowTuple.Create(
                    sourceAddress: "10.0.0.1",
                    destinationAddress: "203.0.113.10",
                    protocol: "tcp"),
                RouteTrace = MinimalTrace(),
            });

        Assert.Equal(SensorObservationCorrelationStatus.Indeterminate, result.Status);
        Assert.Contains(
            result.Findings,
            f => f.Code == SensorObservationCorrelationCodes.InsufficientObservationContext);
    }

    [Fact]
    public void UseCaseValidatesNullQuery()
    {
        CorrelateSensorObservationUseCase useCase = new(new Mfc.UnitTests.Application.Fakes.FakeAuthorizationBoundary());
        Assert.Throws<ArgumentNullException>(() =>
            useCase.ExecuteAsync(
                new CorrelateSensorObservationCommand
                {
                    Actor = "tester",
                    Query = null!,
                }).GetAwaiter().GetResult());
    }

    private static RouteResolutionTrace MinimalTrace(
        string destination = "203.0.113.10",
        string table = "main")
        => new()
        {
            Family = "ipv4",
            DestinationAddress = destination,
            SelectedTable = table,
            Decision = RouteResolutionDecisions.Forward,
            EgressInterfaces = ["ether2"],
            ExecutionPath = RouteResolutionExecutionPaths.Cpu,
            Certainty = RouteResolutionCertainties.Definite,
        };
}
