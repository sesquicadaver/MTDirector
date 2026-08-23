using Mfc.Domain.Routing;

namespace Mfc.Domain.Incident;

/// <summary>Packet-processing stage where the sensor observed the flow (M7.1 §16).</summary>
public enum SensorObservationPoint
{
    Prerouting = 1,
    PostDstNat = 2,
    PostRouting = 3,
    Egress = 4,
}

/// <summary>Outcome of correlating a sensor observation with a route resolution trace.</summary>
public enum SensorObservationCorrelationStatus
{
    Aligned = 1,
    Mismatched = 2,
    SensorBypassed = 3,
    Indeterminate = 4,
}

/// <summary>Input for sensor observation ↔ route trace correlation (M7.3-04).</summary>
public sealed class SensorObservationCorrelationQuery
{
    public required SensorObservationPoint ObservationPoint { get; init; }

    public required FlowTuple OriginalFlow { get; init; }

    public FlowTuple? TranslatedFlow { get; init; }

    public string? IngressInterface { get; init; }

    public string? EgressInterface { get; init; }

    public string? Vrf { get; init; }

    public string? RoutingMark { get; init; }

    public string? SelectedTable { get; init; }

    public RouteResolutionTrace? RouteTrace { get; init; }
}

public sealed class SensorObservationCorrelationFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Resolver output for one sensor observation correlation.</summary>
public sealed class SensorObservationCorrelationResult
{
    public SensorObservationCorrelationStatus Status { get; init; }

    public IReadOnlyList<SensorObservationCorrelationFinding> Findings { get; init; } = [];
}
