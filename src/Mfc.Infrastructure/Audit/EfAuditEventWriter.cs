using System.Security.Cryptography;
using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Audit;

/// <summary>Appends hash-chained audit events. Callers must never pass credential material in payloadJson.</summary>
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

        byte[]? previous = await _db.AuditEvents
            .OrderByDescending(e => e.OccurredAtUtc)
            .Select(e => e.EventHash)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        byte[] eventHash = SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{previous?.Length ?? 0}|{actor}|{action}|{payloadJson}"));

        _db.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Actor = actor.Trim(),
            Action = action.Trim(),
            PayloadJson = payloadJson,
            PreviousEventHash = previous,
            EventHash = eventHash,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
