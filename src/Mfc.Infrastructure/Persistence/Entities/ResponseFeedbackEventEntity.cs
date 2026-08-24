namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Append-only persisted RESPONSE_* feedback event (M7.4-05).</summary>
public sealed class ResponseFeedbackEventEntity
{
    public Guid Id { get; set; }

    public short Kind { get; set; }

    public string EventCode { get; set; } = string.Empty;

    public Guid IncidentId { get; set; }

    public Guid NodeId { get; set; }

    /// <summary>JSON array of device UUID strings.</summary>
    public string DeviceIdsJson { get; set; } = "[]";

    public byte[]? PolicyHash { get; set; }

    public byte[]? ArtifactHash { get; set; }

    public byte[]? PlanHash { get; set; }

    public string? VerificationResults { get; set; }

    public string? RollbackStatus { get; set; }

    public string? ResidualRisk { get; set; }

    public Guid CorrelationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool Immutable { get; set; } = true;
}
