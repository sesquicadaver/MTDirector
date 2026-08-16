namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Desired policy binding (Policy Model §10 / M2-17). Payload hashes stay frozen.</summary>
public sealed class PolicyBindingEntity
{
    public const short ActiveState = 0;

    public const short DisabledState = 1;

    public const short ExpiredPendingReconciliationState = 2;

    public Guid Id { get; set; }

    public short Scope { get; set; }

    public Guid? ScopeId { get; set; }

    public Guid PolicyId { get; set; }

    public Guid DesiredRevisionId { get; set; }

    public Guid AnalysisRunId { get; set; }

    public required byte[] BundleHash { get; set; }

    public short State { get; set; }

    public DateTimeOffset? ValidFromUtc { get; set; }

    public DateTimeOffset? ValidUntilUtc { get; set; }

    public long RowVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
