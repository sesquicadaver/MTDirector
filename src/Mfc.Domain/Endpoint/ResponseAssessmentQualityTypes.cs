using Mfc.Domain.Incident;
using Mfc.Domain.Routing;

namespace Mfc.Domain.Endpoint;

/// <summary>Observation coverage for a response assessment (next-2 §visibility_status).</summary>
public enum AssessmentVisibilityStatus
{
    Full = 1,
    Partial = 2,
    NotObserved = 3,
}

/// <summary>Domain mirror of packet-path observation class (N1-03 input for M7.3-05).</summary>
public enum ObservedPacketPathClass
{
    Unknown = 0,
    CpuFirewall = 1,
    HardwareOffloaded = 2,
    Mixed = 3,
    Indeterminate = 4,
}

/// <summary>Scripted observation inputs for assessment quality evaluation (M7.3-05).</summary>
public sealed class ResponseAssessmentQualityInput
{
    public required ResponseAssessmentFeasibility Feasibility { get; init; }

    public SessionVisibilityStatus? SessionVisibility { get; init; }

    public RouteResolutionTrace? RouteTrace { get; init; }

    public ObservedPacketPathClass PacketPathClass { get; init; } = ObservedPacketPathClass.Unknown;
}

public sealed class ResponseAssessmentQualityFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Derived visibility and confidence for one response assessment (M7.3-05).</summary>
public sealed class ResponseAssessmentQualityResult
{
    public AssessmentVisibilityStatus VisibilityStatus { get; init; }

    public int Confidence { get; init; }

    public IReadOnlyList<ResponseAssessmentQualityFinding> Findings { get; init; } = [];
}
