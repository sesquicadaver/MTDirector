using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Snapshots;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.Infrastructure.Persistence.Snapshots;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Policies;

/// <summary>EF Core content-addressed filter artifact store (M3-07).</summary>
public sealed class EfFilterArtifactStore : IFilterArtifactStore
{
    private readonly MfcDbContext _db;

    public EfFilterArtifactStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<StoredFilterArtifact?> GetByResourceHashAsync(
        Hash256 resourceHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceHash);
        byte[] key = resourceHash.Bytes.ToArray();
        FilterArtifactEntity? entity = await _db.FilterArtifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ResourceHash == key, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToStored(entity, inserted: false);
    }

    public async Task<StoredFilterArtifact> PutIfAbsentAsync(
        RouterOsFilterArtifact artifact,
        CompilationProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(provenance);

        byte[] resourceHashBytes = artifact.ResourceHash.Bytes.ToArray();
        FilterArtifactEntity? existing = await _db.FilterArtifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ResourceHash == resourceHashBytes, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return ToStored(existing, inserted: false);
        }

        BrotliPayloadCodec.EncodedPayload encoded = BrotliPayloadCodec.Encode(artifact.CanonicalBytes.AsMemory());
        if (!encoded.PayloadHash.AsSpan().SequenceEqual(resourceHashBytes))
        {
            throw new InvalidOperationException(
                "Filter artifact resource_hash does not match SHA-256 of canonical bytes.");
        }

        if (encoded.UncompressedSize > FilterArtifactLimits.LayoutV1MaxCanonicalBytes)
        {
            throw new InvalidOperationException(
                $"Filter artifact exceeds max uncompressed size of {FilterArtifactLimits.LayoutV1MaxCanonicalBytes} bytes.");
        }

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        FilterArtifactEntity entity = new()
        {
            ResourceHash = resourceHashBytes,
            ArtifactId = artifact.ArtifactId,
            DeviceId = artifact.DeviceId.Value,
            PhysicalSemanticsHash = artifact.PhysicalSemanticsHash.Bytes.ToArray(),
            CompilerProfileHash = artifact.CompilerProfileHash.Bytes.ToArray(),
            LogicalEffectivePolicyHash = provenance.LogicalEffectivePolicyHash.Bytes.ToArray(),
            DeviceResolvedPolicyHash = provenance.DeviceResolvedPolicyHash.Bytes.ToArray(),
            AnalysisBundleHash = provenance.AnalysisBundleHash.Bytes.ToArray(),
            CapabilityHash = provenance.CapabilityHash.Bytes.ToArray(),
            CompilerVersion = provenance.CompilerVersion,
            CompiledAtUtc = provenance.CompiledAtUtc,
            Compression = (short)encoded.Compression,
            UncompressedSize = encoded.UncompressedSize,
            CompressedPayload = encoded.CompressedPayload,
            CreatedAtUtc = createdAt,
        };

        try
        {
            _db.FilterArtifacts.Add(entity);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToStored(entity, inserted: true);
        }
        catch (DbUpdateException)
        {
            _db.Entry(entity).State = EntityState.Detached;
            FilterArtifactEntity? raced = await _db.FilterArtifacts
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ResourceHash == resourceHashBytes, cancellationToken)
                .ConfigureAwait(false);
            if (raced is not null)
            {
                return ToStored(raced, inserted: false);
            }

            throw;
        }
    }

    private static StoredFilterArtifact ToStored(FilterArtifactEntity entity, bool inserted)
        => new()
        {
            ResourceHash = Hash256.Create(entity.ResourceHash),
            ArtifactId = entity.ArtifactId,
            DeviceId = new DeviceId(entity.DeviceId),
            PhysicalSemanticsHash = Hash256.Create(entity.PhysicalSemanticsHash),
            CompilerProfileHash = Hash256.Create(entity.CompilerProfileHash),
            LogicalEffectivePolicyHash = Hash256.Create(entity.LogicalEffectivePolicyHash),
            DeviceResolvedPolicyHash = Hash256.Create(entity.DeviceResolvedPolicyHash),
            AnalysisBundleHash = Hash256.Create(entity.AnalysisBundleHash),
            CapabilityHash = Hash256.Create(entity.CapabilityHash),
            CompilerVersion = entity.CompilerVersion,
            CompiledAtUtc = entity.CompiledAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UncompressedSize = entity.UncompressedSize,
            Inserted = inserted,
        };
}
