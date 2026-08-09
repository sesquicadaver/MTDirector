using Mfc.Application.Abstractions.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Inventory;

/// <summary>PostgreSQL-backed mutation idempotency using <c>idempotency_records</c>.</summary>
public sealed class EfIdempotencyStore : IIdempotencyStore
{
    private readonly MfcDbContext _db;

    public EfIdempotencyStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IdempotencyLookupResult> TryGetAsync(
        string actor,
        string operation,
        Guid idempotencyKey,
        ReadOnlyMemory<byte> requestHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        string key = FormatKey(idempotencyKey);
        IdempotencyRecordEntity? existing = await _db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.Actor == actor && r.Operation == operation && r.Key == key,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return new IdempotencyLookupResult { Found = false };
        }

        if (!existing.RequestHash.AsSpan().SequenceEqual(requestHash.Span))
        {
            return new IdempotencyLookupResult { Found = true, Conflict = true };
        }

        if (!Guid.TryParse(existing.ResponseRef, out Guid resourceId))
        {
            return new IdempotencyLookupResult { Found = true, Conflict = true };
        }

        return new IdempotencyLookupResult
        {
            Found = true,
            ResourceId = resourceId,
        };
    }

    public async Task SaveAsync(
        string actor,
        string operation,
        Guid idempotencyKey,
        ReadOnlyMemory<byte> requestHash,
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        string key = FormatKey(idempotencyKey);
        _db.IdempotencyRecords.Add(new IdempotencyRecordEntity
        {
            Key = key,
            Actor = actor.Trim(),
            Operation = operation.Trim(),
            RequestHash = requestHash.ToArray(),
            ResponseRef = resourceId.ToString("D"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string FormatKey(Guid idempotencyKey) => idempotencyKey.ToString("D");
}
