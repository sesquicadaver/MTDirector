namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Append-only approval vote (Policy Model §67 / M2-17).</summary>
public sealed class PolicyApprovalEntity
{
    public Guid Id { get; set; }

    public Guid RevisionId { get; set; }

    public Guid AnalysisRunId { get; set; }

    public required byte[] BundleHash { get; set; }

    public Guid ReviewerId { get; set; }

    public bool IsSecurityOwner { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }
}
