namespace Mfc.Domain.Routing;

/// <summary>Scripted latency measurement input for network path evaluation (M7.1-08).</summary>
public sealed class LatencyMeasurement
{
    /// <summary>Observed packet loss percentage (0–100).</summary>
    public required double PacketLossPercent { get; init; }

    /// <summary>Mean round-trip time in milliseconds.</summary>
    public required double RoundTripTimeMs { get; init; }

    /// <summary>Jitter in milliseconds.</summary>
    public required double JitterMs { get; init; }
}

/// <summary>Evaluation input combining profile, trace, baseline, and measurement (M7.1 Spec §13).</summary>
public sealed class NetworkPathLatencyEvaluationInput
{
    public required NetworkPathProfile Profile { get; init; }

    public required RouteResolutionTrace Trace { get; init; }

    public required LatencyMeasurement Measurement { get; init; }

    public RoutePathFingerprint? BaselinePathFingerprint { get; init; }

    public LatencyMeasurement? BaselineMeasurement { get; init; }
}
