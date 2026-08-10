using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.Infrastructure.Persistence.Snapshots;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Policies;

/// <summary>EF Core document-centric policy store (Policy Model §66).</summary>
public sealed class EfPolicyStore : IPolicyStore
{
    private readonly MfcDbContext _db;

    public EfPolicyStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AddPolicyAsync(Policy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _db.Policies.Add(new PolicyEntity
        {
            Id = policy.Id.Value,
            Name = policy.Name.Value,
            Kind = (short)policy.Kind,
            OwnerScope = (short)policy.OwnerScope,
            OwnerId = policy.OwnerId,
            Status = (short)policy.Status,
            RowVersion = (long)policy.RowVersion,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Policy?> GetPolicyAsync(PolicyId id, CancellationToken cancellationToken = default)
    {
        PolicyEntity? entity = await _db.Policies.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomainPolicy(entity);
    }

    public async Task UpdatePolicyAsync(Policy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        PolicyEntity entity = await _db.Policies
            .SingleAsync(p => p.Id == policy.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        entity.Name = policy.Name.Value;
        entity.Status = (short)policy.Status;
        entity.RowVersion = (long)policy.RowVersion;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddRevisionAsync(PolicyRevision revision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);
        BrotliPayloadCodec.EncodedPayload encoded = BrotliPayloadCodec.Encode(revision.CanonicalBytes);
        if (!encoded.PayloadHash.AsSpan().SequenceEqual(revision.ContentHash.Bytes))
        {
            throw new InvalidOperationException(
                "Policy revision content hash must equal SHA-256 of uncompressed canonical bytes before compression.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        _db.PolicyRevisions.Add(new PolicyRevisionEntity
        {
            Id = revision.Id.Value,
            PolicyId = revision.PolicyId.Value,
            RevisionNumber = revision.RevisionNumber,
            SchemaVersion = checked((int)revision.SchemaVersion),
            ContentHash = revision.ContentHash.Bytes.ToArray(),
            ParentContextHash = revision.ParentContextHash?.Bytes.ToArray(),
            State = (short)revision.State,
            CreatedBy = revision.CreatedBy.Value,
            CreatedAtUtc = revision.CreatedAtUtc,
            ApprovedAtUtc = revision.ApprovedAtUtc,
            Compression = (short)encoded.Compression,
            UncompressedSize = encoded.UncompressedSize,
            CompressedPayload = encoded.CompressedPayload,
            UpdatedAtUtc = now,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveRevisionAsync(PolicyRevision revision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);
        PolicyRevisionEntity entity = await _db.PolicyRevisions
            .SingleAsync(r => r.Id == revision.Id.Value, cancellationToken)
            .ConfigureAwait(false);

        short originalState = entity.State;
        bool payloadImmutable = originalState is PolicyRevisionEntity.ApprovedState
            or PolicyRevisionEntity.RejectedState
            or PolicyRevisionEntity.SupersededState
            or PolicyRevisionEntity.RevokedState;

        if (payloadImmutable)
        {
            // Lifecycle-only updates (APPROVED → SUPERSEDED/REVOKED). Payload fields stay untouched.
            entity.State = (short)revision.State;
            entity.ApprovedAtUtc = revision.ApprovedAtUtc;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        BrotliPayloadCodec.EncodedPayload encoded = BrotliPayloadCodec.Encode(revision.CanonicalBytes);
        if (!encoded.PayloadHash.AsSpan().SequenceEqual(revision.ContentHash.Bytes))
        {
            throw new InvalidOperationException(
                "Policy revision content hash must equal SHA-256 of uncompressed canonical bytes before compression.");
        }

        entity.SchemaVersion = checked((int)revision.SchemaVersion);
        entity.ContentHash = revision.ContentHash.Bytes.ToArray();
        entity.ParentContextHash = revision.ParentContextHash?.Bytes.ToArray();
        entity.State = (short)revision.State;
        entity.ApprovedAtUtc = revision.ApprovedAtUtc;
        entity.Compression = (short)encoded.Compression;
        entity.UncompressedSize = encoded.UncompressedSize;
        entity.CompressedPayload = encoded.CompressedPayload;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PolicyRevision?> GetRevisionAsync(
        PolicyRevisionId id,
        CancellationToken cancellationToken = default)
    {
        PolicyRevisionEntity? entity = await _db.PolicyRevisions.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomainRevision(entity);
    }

    public async Task<IReadOnlyList<PolicyRevision>> ListRevisionsAsync(
        PolicyId policyId,
        CancellationToken cancellationToken = default)
    {
        List<PolicyRevisionEntity> rows = await _db.PolicyRevisions.AsNoTracking()
            .Where(r => r.PolicyId == policyId.Value)
            .OrderBy(r => r.RevisionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomainRevision).ToArray();
    }

    public async Task<uint> GetLatestRevisionNumberAsync(
        PolicyId policyId,
        CancellationToken cancellationToken = default)
    {
        long? max = await _db.PolicyRevisions.AsNoTracking()
            .Where(r => r.PolicyId == policyId.Value)
            .Select(r => (long?)r.RevisionNumber)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);
        return max is null ? 0u : checked((uint)max.Value);
    }

    private static Policy ToDomainPolicy(PolicyEntity entity)
        => Policy.Reconstitute(
            new PolicyId(entity.Id),
            NonEmptyName.Create(entity.Name),
            (PolicyKind)entity.Kind,
            (PolicyOwnerScope)entity.OwnerScope,
            entity.OwnerId,
            (PolicyStatus)entity.Status,
            (ulong)entity.RowVersion);

    private static PolicyRevision ToDomainRevision(PolicyRevisionEntity entity)
    {
        byte[] uncompressed = BrotliPayloadCodec.DecodeAndVerify(
            entity.CompressedPayload,
            (SnapshotCompression)entity.Compression,
            entity.UncompressedSize,
            entity.ContentHash);
        return PolicyRevision.Reconstitute(
            new PolicyRevisionId(entity.Id),
            new PolicyId(entity.PolicyId),
            checked((uint)entity.RevisionNumber),
            checked((uint)entity.SchemaVersion),
            Hash256.Create(entity.ContentHash),
            entity.ParentContextHash is null ? null : Hash256.Create(entity.ParentContextHash),
            (PolicyRevisionState)entity.State,
            new UserId(entity.CreatedBy),
            entity.CreatedAtUtc,
            entity.ApprovedAtUtc,
            uncompressed);
    }
}
