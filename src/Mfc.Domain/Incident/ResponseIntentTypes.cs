using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Routing;

namespace Mfc.Domain.Incident;

/// <summary>Typed response action from the external analytics complex (next-2 §ResponseIntent).</summary>
public enum ResponseIntentAction
{
    TemporaryPreStateDeny = 1,
    RevokeTemporaryException = 2,
    RestoreCommittedPolicy = 3,
}

/// <summary>Response urgency declared by the requester (next-2 §ResponseIntent).</summary>
public enum ResponseIntentUrgency
{
    Normal = 1,
    Emergency = 2,
}

/// <summary>Scripted path observation inputs for the feasibility matrix (M7.4-02 / next-2).</summary>
public sealed class ResponseIntentFeasibilityQuery
{
    public required ResponseIntent Intent { get; init; }

    public SessionVisibilityStatus? SessionVisibility { get; init; }

    public ObservedPacketPathClass PacketPathClass { get; init; } = ObservedPacketPathClass.Unknown;

    public RouteResolutionTrace? RouteTrace { get; init; }

    /// <summary>True when traffic stays in the same bridge/VLAN without IP firewall path.</summary>
    public bool L2BridgeVlanBypass { get; init; }

    /// <summary>True when routed container VETH/FORWARD path is proven through CPU firewall.</summary>
    public bool ProvenRoutedContainerForward { get; init; }

    /// <summary>True when an existing FastTrack connection is active for the targeted flow.</summary>
    public bool FastTrackSessionActive { get; init; }
}

public sealed class ResponseIntentFeasibilityFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Feasibility outcome for one <see cref="ResponseIntent"/> (M7.4-02).</summary>
public sealed class ResponseIntentFeasibilityResult
{
    public required ResponseAssessmentFeasibility Feasibility { get; init; }

    public IReadOnlyList<ResponseIntentFeasibilityFinding> Findings { get; init; } = [];
}
