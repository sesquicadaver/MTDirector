namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted policy revision with compressed MFC-CJ1 payload (Policy Model §8 / §66).</summary>
public sealed class PolicyRevisionEntity
{
    /// <summary>APPROVED ordinal used by DbContext immutability guards.</summary>
    public const short ApprovedState = 3;

    public const short RejectedState = 4;

    public const short SupersededState = 5;

    public const short RevokedState = 6;

    public Guid Id { get; set; }

    public Guid PolicyId { get; set; }

    public long RevisionNumber { get; set; }

    public int SchemaVersion { get; set; }

    public required byte[] ContentHash { get; set; }

    public byte[]? ParentContextHash { get; set; }

    public short State { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public Guid? ApprovedAnalysisRunId { get; set; }

    public byte[]? ApprovedBundleHash { get; set; }

    public short Compression { get; set; }

    public long UncompressedSize { get; set; }

    public required byte[] CompressedPayload { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
