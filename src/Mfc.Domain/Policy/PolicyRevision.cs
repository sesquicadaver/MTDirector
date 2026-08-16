using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Immutable-after-approval policy revision aggregate (Policy Model §8–§9).
/// </summary>
public sealed class PolicyRevision
{
    public PolicyRevisionId Id { get; }

    public PolicyId PolicyId { get; }

    public uint RevisionNumber { get; }

    public uint SchemaVersion { get; private set; }

    public Hash256 ContentHash { get; private set; }

    public Hash256? ParentContextHash { get; private set; }

    public PolicyRevisionState State { get; private set; }

    public UserId CreatedBy { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    /// <summary>Analysis run that completed approval; required for desired binding (M2-17).</summary>
    public PolicyAnalysisRunId? ApprovedAnalysisRunId { get; private set; }

    /// <summary>Bundle hash frozen with <see cref="ApprovedAnalysisRunId"/>.</summary>
    public Hash256? ApprovedBundleHash { get; private set; }

    /// <summary>Exact uncompressed MFC-CJ1 bytes; hash is computed over these bytes before compression.</summary>
    public byte[] CanonicalBytes { get; private set; }

    private PolicyRevision(
        PolicyRevisionId id,
        PolicyId policyId,
        uint revisionNumber,
        uint schemaVersion,
        Hash256 contentHash,
        Hash256? parentContextHash,
        PolicyRevisionState state,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? approvedAtUtc,
        byte[] canonicalBytes,
        PolicyAnalysisRunId? approvedAnalysisRunId = null,
        Hash256? approvedBundleHash = null)
    {
        Id = id;
        PolicyId = policyId;
        RevisionNumber = revisionNumber;
        SchemaVersion = schemaVersion;
        ContentHash = contentHash;
        ParentContextHash = parentContextHash;
        State = state;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
        ApprovedAtUtc = approvedAtUtc;
        CanonicalBytes = canonicalBytes;
        ApprovedAnalysisRunId = approvedAnalysisRunId;
        ApprovedBundleHash = approvedBundleHash;
    }

    public static PolicyRevision CreateDraft(
        Policy policy,
        uint revisionNumber,
        PolicyDocument document,
        Hash256? parentContextHash,
        UserId createdBy,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(document);
        if (revisionNumber == 0)
        {
            throw new DomainInvariantException("revision_number must be greater than zero.");
        }

        if (document.Kind != policy.Kind || document.OwnerScope != policy.OwnerScope)
        {
            throw new DomainInvariantException("Draft document kind/owner_scope must match the policy container.");
        }

        ValidateParentContextForKind(policy.Kind, parentContextHash);
        byte[] bytes = PolicyCanonicalWriter.Write(document);
        Hash256 contentHash = PolicyHashing.HashContent(bytes);
        return new PolicyRevision(
            PolicyRevisionId.New(),
            policy.Id,
            revisionNumber,
            document.SchemaVersion,
            contentHash,
            parentContextHash,
            PolicyRevisionState.Draft,
            createdBy,
            NormalizeUtc(createdAtUtc),
            approvedAtUtc: null,
            bytes);
    }

    /// <summary>Rebuilds a revision from persistence (payload already verified against content hash).</summary>
    public static PolicyRevision Reconstitute(
        PolicyRevisionId id,
        PolicyId policyId,
        uint revisionNumber,
        uint schemaVersion,
        Hash256 contentHash,
        Hash256? parentContextHash,
        PolicyRevisionState state,
        UserId createdBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? approvedAtUtc,
        byte[] canonicalBytes,
        PolicyAnalysisRunId? approvedAnalysisRunId = null,
        Hash256? approvedBundleHash = null)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        ArgumentNullException.ThrowIfNull(canonicalBytes);
        if (revisionNumber == 0 || schemaVersion == 0)
        {
            throw new DomainInvariantException("revision_number and schema_version must be greater than zero.");
        }

        if (canonicalBytes.Length == 0)
        {
            throw new DomainInvariantException("Canonical revision bytes must be non-empty.");
        }

        Hash256 actual = PolicyHashing.HashContent(canonicalBytes);
        if (!actual.Equals(contentHash))
        {
            throw new DomainInvariantException("Stored content_hash does not match canonical bytes.");
        }

        if (state == PolicyRevisionState.Approved && approvedAtUtc is null)
        {
            throw new DomainInvariantException("APPROVED revision requires approved_at.");
        }

        if ((approvedAnalysisRunId is null) != (approvedBundleHash is null))
        {
            throw new DomainInvariantException("Approved analysis run id and bundle hash must be set together.");
        }

        return new PolicyRevision(
            id,
            policyId,
            revisionNumber,
            schemaVersion,
            contentHash,
            parentContextHash,
            state,
            createdBy,
            NormalizeUtc(createdAtUtc),
            approvedAtUtc is null ? null : NormalizeUtc(approvedAtUtc.Value),
            canonicalBytes.ToArray(),
            approvedAnalysisRunId,
            approvedBundleHash);
    }

    /// <summary>
    /// Replaces draft document bytes. Allowed for DRAFT; for VALIDATED returns state to DRAFT
    /// and invalidates validation (MVP E2E §20 / Policy Model §9).
    /// </summary>
    public void ReplaceDocument(PolicyDocument document, Hash256? parentContextHash)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (State is not (PolicyRevisionState.Draft or PolicyRevisionState.Validated))
        {
            throw new DomainInvariantException("Only DRAFT (or VALIDATED returning to DRAFT) may be edited.");
        }

        byte[] bytes = PolicyCanonicalWriter.Write(document);
        Hash256 contentHash = PolicyHashing.HashContent(bytes);
        ValidateParentContextForKind(document.Kind, parentContextHash);

        SchemaVersion = document.SchemaVersion;
        CanonicalBytes = bytes;
        ContentHash = contentHash;
        ParentContextHash = parentContextHash;
        State = PolicyRevisionState.Draft;
        ApprovedAtUtc = null;
        ApprovedAnalysisRunId = null;
        ApprovedBundleHash = null;
    }

    public void MarkValidated()
    {
        EnsureTransition(PolicyRevisionState.Draft, PolicyRevisionState.Validated);
        State = PolicyRevisionState.Validated;
    }

    public void SubmitForReview()
    {
        EnsureTransition(PolicyRevisionState.Validated, PolicyRevisionState.InReview);
        State = PolicyRevisionState.InReview;
    }

    public void Approve(DateTimeOffset approvedAtUtc)
        => ApproveCore(approvedAtUtc, analysisRunId: null, bundleHash: null);

    /// <summary>IN_REVIEW → APPROVED, freezing the analysis run that completed review.</summary>
    public void Approve(DateTimeOffset approvedAtUtc, PolicyAnalysisRunId analysisRunId, Hash256 bundleHash)
    {
        ArgumentNullException.ThrowIfNull(bundleHash);
        ApproveCore(approvedAtUtc, analysisRunId, bundleHash);
    }

    private void ApproveCore(
        DateTimeOffset approvedAtUtc,
        PolicyAnalysisRunId? analysisRunId,
        Hash256? bundleHash)
    {
        EnsureTransition(PolicyRevisionState.InReview, PolicyRevisionState.Approved);
        State = PolicyRevisionState.Approved;
        ApprovedAtUtc = NormalizeUtc(approvedAtUtc);
        ApprovedAnalysisRunId = analysisRunId;
        ApprovedBundleHash = bundleHash;
    }

    public void Reject()
    {
        EnsureTransition(PolicyRevisionState.InReview, PolicyRevisionState.Rejected);
        State = PolicyRevisionState.Rejected;
    }

    public void Supersede()
    {
        EnsureTransition(PolicyRevisionState.Approved, PolicyRevisionState.Superseded);
        State = PolicyRevisionState.Superseded;
    }

    public void Revoke()
    {
        EnsureTransition(PolicyRevisionState.Approved, PolicyRevisionState.Revoked);
        State = PolicyRevisionState.Revoked;
    }

    /// <summary>Clones an APPROVED revision into a new DRAFT (Policy Model §9 rule 9).</summary>
    public PolicyRevision CloneToDraft(
        Policy policy,
        uint nextRevisionNumber,
        UserId createdBy,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (State != PolicyRevisionState.Approved)
        {
            throw new DomainInvariantException("Only APPROVED revisions may be cloned into a new DRAFT.");
        }

        if (policy.Id != PolicyId)
        {
            throw new DomainInvariantException("Clone target policy must match the source revision policy_id.");
        }

        if (nextRevisionNumber <= RevisionNumber)
        {
            throw new DomainInvariantException("Clone revision_number must be greater than the source revision.");
        }

        byte[] bytes = CanonicalBytes.ToArray();
        return new PolicyRevision(
            PolicyRevisionId.New(),
            PolicyId,
            nextRevisionNumber,
            SchemaVersion,
            ContentHash,
            ParentContextHash,
            PolicyRevisionState.Draft,
            createdBy,
            NormalizeUtc(createdAtUtc),
            approvedAtUtc: null,
            bytes);
    }

    private void EnsureTransition(PolicyRevisionState expectedFrom, PolicyRevisionState _)
    {
        if (State != expectedFrom)
        {
            throw new DomainInvariantException(
                $"Invalid lifecycle transition: expected {PolicyCanonicalWriter.FormatRevisionState(expectedFrom)}, " +
                $"actual {PolicyCanonicalWriter.FormatRevisionState(State)}.");
        }
    }

    private static void ValidateParentContextForKind(PolicyKind kind, Hash256? parentContextHash)
    {
        if (kind == PolicyKind.CompanyBaseline)
        {
            if (parentContextHash is not null)
            {
                throw new DomainInvariantException("COMPANY_BASELINE must not set parent_context_hash.");
            }

            return;
        }

        if (parentContextHash is null)
        {
            throw new DomainInvariantException($"{PolicyCanonicalWriter.FormatKind(kind)} requires parent_context_hash.");
        }
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.ToUniversalTime();
}
