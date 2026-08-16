using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Append-only approval vote. Never updated or deleted (Policy Model §67).</summary>
public sealed class PolicyApproval
{
    public PolicyApprovalId Id { get; }

    public PolicyRevisionId RevisionId { get; }

    public PolicyAnalysisRunId AnalysisRunId { get; }

    public Hash256 BundleHash { get; }

    public UserId ReviewerId { get; }

    public bool IsSecurityOwner { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    private PolicyApproval(
        PolicyApprovalId id,
        PolicyRevisionId revisionId,
        PolicyAnalysisRunId analysisRunId,
        Hash256 bundleHash,
        UserId reviewerId,
        bool isSecurityOwner,
        DateTimeOffset recordedAtUtc)
    {
        Id = id;
        RevisionId = revisionId;
        AnalysisRunId = analysisRunId;
        BundleHash = bundleHash;
        ReviewerId = reviewerId;
        IsSecurityOwner = isSecurityOwner;
        RecordedAtUtc = recordedAtUtc;
    }

    /// <summary>Records one reviewer vote against an exact analysis bundle hash.</summary>
    public static PolicyApproval Create(
        PolicyRevisionId revisionId,
        PolicyAnalysisRunId analysisRunId,
        Hash256 bundleHash,
        UserId reviewerId,
        bool isSecurityOwner,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(bundleHash);
        return new PolicyApproval(
            PolicyApprovalId.New(),
            revisionId,
            analysisRunId,
            bundleHash,
            reviewerId,
            isSecurityOwner,
            recordedAtUtc.ToUniversalTime());
    }

    /// <summary>Rebuilds a vote from persistence.</summary>
    public static PolicyApproval Reconstitute(
        PolicyApprovalId id,
        PolicyRevisionId revisionId,
        PolicyAnalysisRunId analysisRunId,
        Hash256 bundleHash,
        UserId reviewerId,
        bool isSecurityOwner,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(bundleHash);
        return new PolicyApproval(
            id,
            revisionId,
            analysisRunId,
            bundleHash,
            reviewerId,
            isSecurityOwner,
            recordedAtUtc.ToUniversalTime());
    }
}
