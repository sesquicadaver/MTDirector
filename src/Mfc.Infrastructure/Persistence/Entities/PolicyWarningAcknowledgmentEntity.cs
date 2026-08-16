namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Append-only warning acknowledgment (Policy Model §67 / M2-17).</summary>
public sealed class PolicyWarningAcknowledgmentEntity
{
    public Guid Id { get; set; }

    public Guid AnalysisRunId { get; set; }

    public required byte[] WarningHash { get; set; }

    public Guid AcknowledgedBy { get; set; }

    public DateTimeOffset AcknowledgedAtUtc { get; set; }
}
