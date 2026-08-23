namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted incident response assessment (M7.2-03).</summary>
public sealed class ResponseAssessmentEntity
{
    public Guid AssessmentId { get; set; }

    public Guid IncidentId { get; set; }

    public Guid EndpointId { get; set; }

    public Guid PresenceId { get; set; }

    public Guid EnforcementNodeId { get; set; }

    public int Feasibility { get; set; }

    public int VisibilityStatus { get; set; }

    public int Confidence { get; set; }

    public int Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? InvalidatedAt { get; set; }

    public string? InvalidationReason { get; set; }
}
