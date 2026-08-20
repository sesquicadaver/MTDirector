namespace Mfc.Application.Models;

/// <summary>Application view of one append-only audit event (M6-04).</summary>
public sealed class AuditEventView
{
    public required Guid Id { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required string Actor { get; init; }

    public required string Action { get; init; }

    public required string PayloadJson { get; init; }
}
