using System.Globalization;
using Mfc.Domain.Endpoint;

namespace Mfc.Domain.Policy;

/// <summary>Typed incident deny overlay revision metadata (next-2 §INCIDENT_DENY_OVERLAY).</summary>
public sealed class IncidentDenyOverlayMetadata
{
    public IncidentId IncidentId { get; }

    public Guid NodeId { get; }

    public DateTimeOffset ExpiresAt { get; }

    public string Reason { get; }

    public IReadOnlyList<string> EvidenceRefs { get; }

    private IncidentDenyOverlayMetadata(
        IncidentId incidentId,
        Guid nodeId,
        DateTimeOffset expiresAt,
        string reason,
        IReadOnlyList<string> evidenceRefs)
    {
        IncidentId = incidentId;
        NodeId = nodeId;
        ExpiresAt = expiresAt;
        Reason = reason;
        EvidenceRefs = evidenceRefs;
    }

    /// <summary>Creates metadata and enforces finite expiry, reason, and evidence references.</summary>
    public static IncidentDenyOverlayMetadata Create(
        IncidentId incidentId,
        Guid nodeId,
        DateTimeOffset expiresAt,
        string reason,
        IEnumerable<string> evidenceRefs)
    {
        if (incidentId.Value == Guid.Empty)
        {
            throw new DomainInvariantException("INCIDENT_DENY_OVERLAY incident_id must be a concrete UUID.");
        }

        if (nodeId == Guid.Empty)
        {
            throw new DomainInvariantException("INCIDENT_DENY_OVERLAY node_id must be a concrete UUID.");
        }

        DateTimeOffset expiry = NormalizeUtc(expiresAt);
        if (expiry == DateTimeOffset.MaxValue || expiry == DateTimeOffset.MinValue)
        {
            throw new DomainInvariantException("INCIDENT_DENY_OVERLAY expires_at must be finite.");
        }

        string trimmedReason = RequireNonEmpty(reason, "reason");
        IReadOnlyList<string> refs = NormalizeEvidenceRefs(evidenceRefs);
        return new IncidentDenyOverlayMetadata(incidentId, nodeId, expiry, trimmedReason, refs);
    }

    /// <summary>True when <paramref name="utcNow"/> is at or after <see cref="ExpiresAt"/> (inclusive skip).</summary>
    public bool IsExpired(DateTimeOffset utcNow) => NormalizeUtc(utcNow) >= ExpiresAt;

    public static string FormatTimestamp(DateTimeOffset value)
        => NormalizeUtc(value).ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset ParseTimestamp(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainInvariantException($"{label} must be a non-empty UTC timestamp.");
        }

        if (!DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset parsed))
        {
            throw new DomainInvariantException($"{label} must be a round-trip UTC timestamp.");
        }

        return NormalizeUtc(parsed);
    }

    private static List<string> NormalizeEvidenceRefs(IEnumerable<string> evidenceRefs)
    {
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        List<string> normalized = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string reference in evidenceRefs)
        {
            string trimmed = RequireNonEmpty(reference, "evidence_refs");
            if (!seen.Add(trimmed))
            {
                throw new DomainInvariantException("INCIDENT_DENY_OVERLAY evidence_refs must be unique.");
            }

            normalized.Add(trimmed);
        }

        if (normalized.Count == 0)
        {
            throw new DomainInvariantException("INCIDENT_DENY_OVERLAY requires at least one evidence reference.");
        }

        normalized.Sort(StringComparer.Ordinal);
        return normalized;
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value) => value.ToUniversalTime();

    private static string RequireNonEmpty(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainInvariantException($"INCIDENT_DENY_OVERLAY {label} is required.");
        }

        return value.Trim();
    }
}
