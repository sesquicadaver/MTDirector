namespace Mfc.Domain.Incident;

/// <summary>How completely connection-tracking context was observed (next-2 §2).</summary>
public enum SessionVisibilityStatus
{
    Full = 1,
    Partial = 2,
    NotObserved = 3,
}

/// <summary>One on-demand connection-tracking row (next-2 §2).</summary>
public sealed class ConnectionTrackingEntryFact
{
    public required string Protocol { get; init; }

    public required string OriginalSourceAddress { get; init; }

    public ushort? OriginalSourcePort { get; init; }

    public required string OriginalDestinationAddress { get; init; }

    public ushort? OriginalDestinationPort { get; init; }

    public string? ReplySourceAddress { get; init; }

    public ushort? ReplySourcePort { get; init; }

    public string? ReplyDestinationAddress { get; init; }

    public ushort? ReplyDestinationPort { get; init; }

    public string? ConnectionState { get; init; }

    public string? Timeout { get; init; }

    public bool SrcNatActive { get; init; }

    public bool DstNatActive { get; init; }

    public bool FastTrack { get; init; }

    public bool HwOffload { get; init; }

    public string? ConnectionMark { get; init; }

    public string? RoutingMark { get; init; }
}

/// <summary>Scripted connection-tracking snapshot for on-demand incident lookup (M7.3-03).</summary>
public sealed class ConnectionTrackingSnapshot
{
    public IReadOnlyList<ConnectionTrackingEntryFact> Entries { get; init; } = [];
}

/// <summary>On-demand session lookup keyed by original flow tuple.</summary>
public sealed class IncidentSessionContextQuery
{
    public required FlowTuple OriginalFlow { get; init; }
}

public sealed class IncidentSessionContextFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Resolved on-demand session context for incident correlation (M7.3-03).</summary>
public sealed class IncidentSessionContext
{
    public required string Protocol { get; init; }

    public required FlowTuple OriginalFlow { get; init; }

    public FlowTuple? ReplyFlow { get; init; }

    public string? ConnectionState { get; init; }

    public string? Timeout { get; init; }

    public bool SrcNatActive { get; init; }

    public bool DstNatActive { get; init; }

    public bool FastTrack { get; init; }

    public bool HwOffload { get; init; }

    public string? ConnectionMark { get; init; }

    public string? RoutingMark { get; init; }

    public SessionVisibilityStatus VisibilityStatus { get; init; }
}

/// <summary>Resolver output for one on-demand session lookup.</summary>
public sealed class IncidentSessionContextResult
{
    public IncidentSessionContext? Session { get; init; }

    public SessionVisibilityStatus VisibilityStatus { get; init; }

    public IReadOnlyList<IncidentSessionContextFinding> Findings { get; init; } = [];
}
