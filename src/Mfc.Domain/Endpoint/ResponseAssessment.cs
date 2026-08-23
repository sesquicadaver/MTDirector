using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Endpoint;

/// <summary>Feasibility of incident response on the current route path (next-2 §ResponseAssessment).</summary>
public enum ResponseAssessmentFeasibility
{
    FullyEnforceable = 1,
    NewConnectionsOnly = 2,
    NotEnforceableByIpFilter = 3,
    Indeterminate = 4,
}

/// <summary>Lifecycle status for a persisted response assessment (M7.2-03).</summary>
public enum ResponseAssessmentStatus
{
    Active = 1,
    Invalidated = 2,
}

/// <summary>
/// Controller-side assessment bound to one endpoint presence (M7.2-03 / M7.1 §15).
/// Invalidated on endpoint mobility while an incident remains active.
/// </summary>
public sealed class ResponseAssessment
{
    public AssessmentId AssessmentId { get; }

    public IncidentId IncidentId { get; }

    public EndpointId EndpointId { get; }

    public PresenceId PresenceId { get; }

    public NodeId EnforcementNodeId { get; }

    public ResponseAssessmentFeasibility Feasibility { get; }

    public AssessmentVisibilityStatus VisibilityStatus { get; }

    public int Confidence { get; }

    public ResponseAssessmentStatus Status { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? InvalidatedAt { get; }

    public string? InvalidationReason { get; }

    private ResponseAssessment(
        AssessmentId assessmentId,
        IncidentId incidentId,
        EndpointId endpointId,
        PresenceId presenceId,
        NodeId enforcementNodeId,
        ResponseAssessmentFeasibility feasibility,
        AssessmentVisibilityStatus visibilityStatus,
        int confidence,
        ResponseAssessmentStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? invalidatedAt,
        string? invalidationReason)
    {
        AssessmentId = assessmentId;
        IncidentId = incidentId;
        EndpointId = endpointId;
        PresenceId = presenceId;
        EnforcementNodeId = enforcementNodeId;
        Feasibility = feasibility;
        VisibilityStatus = visibilityStatus;
        Confidence = confidence;
        Status = status;
        CreatedAt = createdAt.ToUniversalTime();
        InvalidatedAt = invalidatedAt?.ToUniversalTime();
        InvalidationReason = invalidationReason;
    }

    public bool IsActive => Status == ResponseAssessmentStatus.Active;

    /// <summary>Creates an active assessment for the current endpoint presence.</summary>
    public static ResponseAssessment CreateActive(
        IncidentId incidentId,
        EndpointId endpointId,
        PresenceId presenceId,
        NodeId enforcementNodeId,
        ResponseAssessmentFeasibility feasibility,
        DateTimeOffset createdAt,
        AssessmentId? assessmentId = null,
        ResponseAssessmentQualityInput? qualityInput = null)
    {
        if (incidentId.Value == Guid.Empty)
        {
            throw new DomainInvariantException("incident_id is required.");
        }

        ResponseAssessmentQualityResult quality = ResponseAssessmentQualityEvaluator.Evaluate(
            qualityInput ?? new ResponseAssessmentQualityInput { Feasibility = feasibility });

        return new ResponseAssessment(
            assessmentId ?? AssessmentId.New(),
            incidentId,
            endpointId,
            presenceId,
            enforcementNodeId,
            feasibility,
            quality.VisibilityStatus,
            quality.Confidence,
            ResponseAssessmentStatus.Active,
            createdAt,
            invalidatedAt: null,
            invalidationReason: null);
    }

    /// <summary>Reconstitutes a persisted assessment.</summary>
    public static ResponseAssessment Reconstitute(
        AssessmentId assessmentId,
        IncidentId incidentId,
        EndpointId endpointId,
        PresenceId presenceId,
        NodeId enforcementNodeId,
        ResponseAssessmentFeasibility feasibility,
        AssessmentVisibilityStatus visibilityStatus,
        int confidence,
        ResponseAssessmentStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? invalidatedAt = null,
        string? invalidationReason = null)
    {
        if (confidence is < 0 or > 100)
        {
            throw new DomainInvariantException("confidence must be between 0 and 100.");
        }

        return new ResponseAssessment(
            assessmentId,
            incidentId,
            endpointId,
            presenceId,
            enforcementNodeId,
            feasibility,
            visibilityStatus,
            confidence,
            status,
            createdAt,
            invalidatedAt,
            NormalizeOptional(invalidationReason));
    }

    /// <summary>Invalidates the assessment because endpoint mobility changed routing context.</summary>
    public ResponseAssessment Invalidate(DateTimeOffset invalidatedAt, string reason)
    {
        if (!IsActive)
        {
            throw new DomainInvariantException(
                $"Assessment '{AssessmentId}' is already invalidated.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainInvariantException("Invalidation reason is required.");
        }

        DateTimeOffset at = invalidatedAt.ToUniversalTime();
        if (at < CreatedAt)
        {
            throw new DomainInvariantException("Invalidated_at must be greater than or equal to created_at.");
        }

        return new ResponseAssessment(
            AssessmentId,
            IncidentId,
            EndpointId,
            PresenceId,
            EnforcementNodeId,
            Feasibility,
            VisibilityStatus,
            Confidence,
            ResponseAssessmentStatus.Invalidated,
            CreatedAt,
            at,
            reason.Trim());
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
