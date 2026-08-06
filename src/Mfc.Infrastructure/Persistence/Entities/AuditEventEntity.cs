namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Append-only audit event with hash-chain fields. No application update/delete path.
/// </summary>
public sealed class AuditEventEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public required string Actor { get; set; }

    public required string Action { get; set; }

    /// <summary>JSON payload stored as jsonb. Must not contain secrets.</summary>
    public required string PayloadJson { get; set; }

    public byte[]? PreviousEventHash { get; set; }

    public required byte[] EventHash { get; set; }
}
