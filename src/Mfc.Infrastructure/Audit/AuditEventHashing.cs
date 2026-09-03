using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Mfc.Infrastructure.Audit;

/// <summary>
/// Canonical audit EventHash preimage (SEC-03). Includes predecessor <em>bytes</em> (not length),
/// plus stable event identity fields.
/// </summary>
public static class AuditEventHashing
{
    /// <summary>
    /// Computes SHA-256 over UTF-8 of
    /// <c>{previousHex}|{eventId:D}|{actor}|{action}|{payloadJson}</c>
    /// where <c>previousHex</c> is lowercase hex of the predecessor EventHash, or empty for genesis.
    /// </summary>
    public static byte[] Compute(
        byte[]? previousEventHash,
        Guid eventId,
        string actor,
        string action,
        string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id must be non-empty.", nameof(eventId));
        }

        if (previousEventHash is not null && previousEventHash.Length != 32)
        {
            throw new ArgumentException("Previous EventHash must be 32 bytes when present.", nameof(previousEventHash));
        }

        string previousHex = previousEventHash is null
            ? string.Empty
            : Convert.ToHexString(previousEventHash).ToLowerInvariant();
        string preimage = string.Create(
            CultureInfo.InvariantCulture,
            $"{previousHex}|{eventId:D}|{actor.Trim()}|{action.Trim()}|{payloadJson}");
        return SHA256.HashData(Encoding.UTF8.GetBytes(preimage));
    }
}
