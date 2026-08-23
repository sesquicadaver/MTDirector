using Mfc.Domain.Endpoint;

namespace Mfc.Application.Models;

/// <summary>Application view of a response assessment (M7.2-03).</summary>
public sealed class ResponseAssessmentView
{
    public required Guid AssessmentId { get; init; }

    public required Guid IncidentId { get; init; }

    public required Guid EndpointId { get; init; }

    public required Guid PresenceId { get; init; }

    public required Guid EnforcementNodeId { get; init; }

    public required string Feasibility { get; init; }

    public required string VisibilityStatus { get; init; }

    public required int Confidence { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? InvalidatedAtUtc { get; init; }

    public string? InvalidationReason { get; init; }

    public static ResponseAssessmentView FromDomain(ResponseAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        return new ResponseAssessmentView
        {
            AssessmentId = assessment.AssessmentId.Value,
            IncidentId = assessment.IncidentId.Value,
            EndpointId = assessment.EndpointId.Value,
            PresenceId = assessment.PresenceId.Value,
            EnforcementNodeId = assessment.EnforcementNodeId.Value,
            Feasibility = assessment.Feasibility.ToString(),
            VisibilityStatus = assessment.VisibilityStatus.ToString(),
            Confidence = assessment.Confidence,
            Status = assessment.Status.ToString(),
            CreatedAtUtc = assessment.CreatedAt,
            InvalidatedAtUtc = assessment.InvalidatedAt,
            InvalidationReason = assessment.InvalidationReason,
        };
    }
}

/// <summary>Result of opening or migrating endpoint presence (M7.2-02 + M7.2-03).</summary>
public sealed class EndpointPresenceUpsertResultView
{
    public required EndpointRoutingContextView RoutingContext { get; init; }

    public ResponseAssessmentView? InvalidatedAssessment { get; init; }

    public Guid? EnforcementNodeId { get; init; }

    public bool AutoDeploySuppressed { get; init; }
}
