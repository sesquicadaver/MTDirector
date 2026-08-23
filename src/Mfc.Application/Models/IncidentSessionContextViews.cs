using Mfc.Domain.Incident;

namespace Mfc.Application.Models;

public sealed class IncidentSessionContextView
{
    public required string Protocol { get; init; }

    public required FlowTupleView OriginalFlow { get; init; }

    public FlowTupleView? ReplyFlow { get; init; }

    public string? ConnectionState { get; init; }

    public string? Timeout { get; init; }

    public bool SrcNatActive { get; init; }

    public bool DstNatActive { get; init; }

    public bool FastTrack { get; init; }

    public bool HwOffload { get; init; }

    public string? ConnectionMark { get; init; }

    public string? RoutingMark { get; init; }

    public required string VisibilityStatus { get; init; }

    public static IncidentSessionContextView FromDomain(IncidentSessionContext session)
    {
        ArgumentNullException.ThrowIfNull(session);
        FlowTupleView original = FlowTupleView.FromDomain(session.OriginalFlow)
            ?? throw new InvalidOperationException("Session original flow is required.");
        return new IncidentSessionContextView
        {
            Protocol = session.Protocol,
            OriginalFlow = original,
            ReplyFlow = FlowTupleView.FromDomain(session.ReplyFlow),
            ConnectionState = session.ConnectionState,
            Timeout = session.Timeout,
            SrcNatActive = session.SrcNatActive,
            DstNatActive = session.DstNatActive,
            FastTrack = session.FastTrack,
            HwOffload = session.HwOffload,
            ConnectionMark = session.ConnectionMark,
            RoutingMark = session.RoutingMark,
            VisibilityStatus = session.VisibilityStatus.ToString(),
        };
    }
}

public sealed class IncidentSessionContextFindingView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

public sealed class IncidentSessionContextResultView
{
    public IncidentSessionContextView? Session { get; init; }

    public required string VisibilityStatus { get; init; }

    public required IReadOnlyList<IncidentSessionContextFindingView> Findings { get; init; }

    public static IncidentSessionContextResultView FromResult(IncidentSessionContextResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new IncidentSessionContextResultView
        {
            Session = result.Session is null ? null : IncidentSessionContextView.FromDomain(result.Session),
            VisibilityStatus = result.VisibilityStatus.ToString(),
            Findings = result.Findings
                .Select(static f => new IncidentSessionContextFindingView
                {
                    Code = f.Code,
                    Message = f.Message,
                    Subject = f.Subject,
                })
                .ToArray(),
        };
    }
}
