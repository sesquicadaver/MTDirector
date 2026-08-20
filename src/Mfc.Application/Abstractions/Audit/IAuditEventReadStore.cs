namespace Mfc.Application.Abstractions.Audit;

/// <summary>Read-only access to append-only audit events (newest first).</summary>
public interface IAuditEventReadStore
{
    /// <summary>Returns up to <paramref name="limit"/> newest events (OccurredAtUtc desc, Id desc).</summary>
    Task<IReadOnlyList<AuditEventRecord>> ListNewestAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>Immutable audit event projection for Application use cases.</summary>
public sealed class AuditEventRecord
{
    public required Guid Id { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required string Actor { get; init; }

    public required string Action { get; init; }

    public required string PayloadJson { get; init; }
}
