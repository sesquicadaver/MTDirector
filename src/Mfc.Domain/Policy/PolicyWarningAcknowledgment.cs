using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Append-only acknowledgment of an exact warning hash (Policy Model §63 rule 7).</summary>
public sealed class PolicyWarningAcknowledgment
{
    public PolicyWarningAcknowledgmentId Id { get; }

    public PolicyAnalysisRunId AnalysisRunId { get; }

    public Hash256 WarningHash { get; }

    public UserId AcknowledgedBy { get; }

    public DateTimeOffset AcknowledgedAtUtc { get; }

    private PolicyWarningAcknowledgment(
        PolicyWarningAcknowledgmentId id,
        PolicyAnalysisRunId analysisRunId,
        Hash256 warningHash,
        UserId acknowledgedBy,
        DateTimeOffset acknowledgedAtUtc)
    {
        Id = id;
        AnalysisRunId = analysisRunId;
        WarningHash = warningHash;
        AcknowledgedBy = acknowledgedBy;
        AcknowledgedAtUtc = acknowledgedAtUtc;
    }

    /// <summary>Creates an insert-only acknowledgment bound to one analysis run.</summary>
    public static PolicyWarningAcknowledgment Create(
        PolicyAnalysisRunId analysisRunId,
        Hash256 warningHash,
        UserId acknowledgedBy,
        DateTimeOffset acknowledgedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(warningHash);
        return new PolicyWarningAcknowledgment(
            PolicyWarningAcknowledgmentId.New(),
            analysisRunId,
            warningHash,
            acknowledgedBy,
            acknowledgedAtUtc.ToUniversalTime());
    }

    /// <summary>Rebuilds an acknowledgment from persistence.</summary>
    public static PolicyWarningAcknowledgment Reconstitute(
        PolicyWarningAcknowledgmentId id,
        PolicyAnalysisRunId analysisRunId,
        Hash256 warningHash,
        UserId acknowledgedBy,
        DateTimeOffset acknowledgedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(warningHash);
        return new PolicyWarningAcknowledgment(
            id,
            analysisRunId,
            warningHash,
            acknowledgedBy,
            acknowledgedAtUtc.ToUniversalTime());
    }
}
