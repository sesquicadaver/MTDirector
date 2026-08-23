using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.Domain.Incident;

/// <summary>Scripted bind input linking one incident signal to one response assessment (M7.3-06).</summary>
public sealed class IncidentResponseAssessmentQuery
{
    public required IncidentSignal Signal { get; init; }

    public required EndpointId EndpointId { get; init; }

    public required PresenceId PresenceId { get; init; }

    public required NodeId EnforcementNodeId { get; init; }

    public required DateTimeOffset AssessedAt { get; init; }

    public SessionVisibilityStatus? SessionVisibility { get; init; }

    public RouteResolutionTrace? RouteTrace { get; init; }

    public ObservedPacketPathClass PacketPathClass { get; init; } = ObservedPacketPathClass.Unknown;

    public ResponseAssessmentFeasibility? FeasibilityOverride { get; init; }
}

public sealed class IncidentResponseAssessmentFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Contract output binding one normalized signal to one response assessment.</summary>
public sealed class IncidentResponseAssessmentBinding
{
    public required IncidentId IncidentId { get; init; }

    public required FlowTuple CorrelationFlow { get; init; }

    public required ResponseAssessment Assessment { get; init; }

    public IReadOnlyList<IncidentResponseAssessmentFinding> Findings { get; init; } = [];
}
