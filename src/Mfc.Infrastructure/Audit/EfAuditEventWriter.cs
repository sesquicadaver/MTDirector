using System.Data;
using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mfc.Infrastructure.Audit;

/// <summary>
/// Appends hash-chained audit events (SEC-03). Callers must never pass credential material in payloadJson.
/// When no ambient transaction exists, appends under Serializable + advisory lock (SEC-03).
/// When called inside <see cref="IUnitOfWork"/>, joins the ambient transaction (SEC-05).
/// </summary>
public sealed class EfAuditEventWriter : IAuditEventWriter
{
    private readonly MfcDbContext _db;

    public EfAuditEventWriter(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AppendAsync(
        string actor,
        string action,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        AssertNoCredentialLeak(payloadJson);

        string trimmedActor = actor.Trim();
        string trimmedAction = action.Trim();

        IDbContextTransaction? ambient = _db.Database.CurrentTransaction;
        bool ownsTransaction = ambient is null;
        IDbContextTransaction? tx = ambient;
        if (ownsTransaction)
        {
            tx = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            // Session-scoped xact advisory lock serializes tip selection across writers (SEC-03).
            await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(87201403)", cancellationToken)
                .ConfigureAwait(false);

            byte[]? previous = await _db.AuditEvents
                .OrderByDescending(e => e.OccurredAtUtc)
                .ThenByDescending(e => e.Id)
                .Select(e => e.EventHash)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            Guid eventId = Guid.NewGuid();
            DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
            byte[] eventHash = AuditEventHashing.Compute(
                previous,
                eventId,
                trimmedActor,
                trimmedAction,
                payloadJson);

            _db.AuditEvents.Add(new AuditEventEntity
            {
                Id = eventId,
                OccurredAtUtc = occurredAt,
                Actor = trimmedActor,
                Action = trimmedAction,
                PayloadJson = payloadJson,
                PreviousEventHash = previous,
                EventHash = eventHash,
            });

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (ownsTransaction)
            {
                await tx!.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            if (ownsTransaction && tx is not null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (ownsTransaction && tx is not null)
            {
                await tx.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static void AssertNoCredentialLeak(string payloadJson)
    {
        using JsonDocument doc = JsonDocument.Parse(payloadJson);
        ForbidSensitiveNames(doc.RootElement);
    }

    private static void ForbidSensitiveNames(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string name = property.Name.ToLowerInvariant();
                    if (name.Contains("password", StringComparison.Ordinal)
                        || name.Contains("ciphertext", StringComparison.Ordinal)
                        || name is "pwd" or "credential" or "secret" or "plaintext")
                    {
                        throw new InvalidOperationException(
                            "Audit payload must not contain credential-related fields.");
                    }

                    ForbidSensitiveNames(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    ForbidSensitiveNames(item);
                }

                break;
        }
    }
}
