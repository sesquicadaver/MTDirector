using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mfc.Application.Abstractions.Persistence;

namespace Mfc.Application.Common;

/// <summary>Maps string actors onto durable GUID keys (capture ops / idempotency).</summary>
public static class ActorKey
{
    public static Guid FromActor(string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(actor.Trim()));
        return new Guid(hash.AsSpan(0, 16));
    }
}

/// <summary>Shared helpers for mutation idempotency keys and request digests.</summary>
internal static class IdempotencySupport
{
    public static ApplicationError? ValidateKey(Guid idempotencyKey)
    {
        if (idempotencyKey == Guid.Empty)
        {
            return ApplicationError.Validation("IdempotencyKey must be a non-empty GUID.");
        }

        return null;
    }

    public static byte[] HashRequest(object payload)
    {
        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(payload);
        return SHA256.HashData(utf8);
    }

    public static async Task<ApplicationResult<T>?> TryReplayAsync<T>(
        IIdempotencyStore store,
        string actor,
        string operation,
        Guid idempotencyKey,
        ReadOnlyMemory<byte> requestHash,
        Func<Guid, CancellationToken, Task<ApplicationResult<T>>> loadByResourceId,
        CancellationToken cancellationToken)
    {
        IdempotencyLookupResult lookup = await store
            .TryGetAsync(actor, operation, idempotencyKey, requestHash, cancellationToken)
            .ConfigureAwait(false);
        if (lookup.Conflict)
        {
            return ApplicationResults.Fail(
                ApplicationError.Conflict("Idempotency key was reused with a different request payload."));
        }

        if (!lookup.Found || lookup.ResourceId is null)
        {
            return null;
        }

        return await loadByResourceId(lookup.ResourceId.Value, cancellationToken).ConfigureAwait(false);
    }
}
