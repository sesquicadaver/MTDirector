using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;

namespace Mfc.Application.Models;

public sealed class ResponseIntentFeasibilityFindingView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

public sealed class ResponseIntentFeasibilityView
{
    public required Guid IncidentId { get; init; }

    public required Guid NodeId { get; init; }

    public required ResponseIntentAction Action { get; init; }

    public required ResponseAssessmentFeasibility Feasibility { get; init; }

    public required IReadOnlyList<ResponseIntentFeasibilityFindingView> Findings { get; init; }

    public static ResponseIntentFeasibilityView FromResult(
        ResponseIntent intent,
        ResponseIntentFeasibilityResult result)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(result);
        return new ResponseIntentFeasibilityView
        {
            IncidentId = intent.IncidentId.Value,
            NodeId = intent.NodeId.Value,
            Action = intent.Action,
            Feasibility = result.Feasibility,
            Findings = result.Findings
                .Select(static f => new ResponseIntentFeasibilityFindingView
                {
                    Code = f.Code,
                    Message = f.Message,
                    Subject = f.Subject,
                })
                .ToArray(),
        };
    }
}
