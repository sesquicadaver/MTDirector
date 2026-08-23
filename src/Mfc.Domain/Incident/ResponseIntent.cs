using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Incident;

/// <summary>
/// Typed response intent from the external analytics complex (next-2 §ResponseIntent / M7.4-02).
/// Controller never receives raw RouterOS commands through this surface.
/// </summary>
public sealed class ResponseIntent
{
    public IncidentId IncidentId { get; }

    public NodeId NodeId { get; }

    public ResponseIntentAction Action { get; }

    public TrafficPredicate Selector { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public ResponseIntentUrgency Urgency { get; }

    public IReadOnlyList<string> EvidenceRefs { get; }

    public string RequestedBy { get; }

    public Guid IdempotencyKey { get; }

    private ResponseIntent(
        IncidentId incidentId,
        NodeId nodeId,
        ResponseIntentAction action,
        TrafficPredicate selector,
        DateTimeOffset? expiresAt,
        ResponseIntentUrgency urgency,
        IReadOnlyList<string> evidenceRefs,
        string requestedBy,
        Guid idempotencyKey)
    {
        IncidentId = incidentId;
        NodeId = nodeId;
        Action = action;
        Selector = selector;
        ExpiresAt = expiresAt;
        Urgency = urgency;
        EvidenceRefs = evidenceRefs;
        RequestedBy = requestedBy;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Creates and validates a response intent.</summary>
    public static ResponseIntent Create(
        IncidentId incidentId,
        NodeId nodeId,
        ResponseIntentAction action,
        TrafficPredicate selector,
        DateTimeOffset? expiresAt,
        ResponseIntentUrgency urgency,
        IEnumerable<string> evidenceRefs,
        string requestedBy,
        Guid idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(evidenceRefs);

        if (incidentId.Value == Guid.Empty)
        {
            throw new DomainInvariantException("incident_id must be a concrete UUID.");
        }

        if (nodeId.Value == Guid.Empty)
        {
            throw new DomainInvariantException("node_id must be a concrete UUID.");
        }

        if (idempotencyKey == Guid.Empty)
        {
            throw new DomainInvariantException("idempotency_key must be a concrete UUID.");
        }

        string principal = RequireNonEmpty(requestedBy, "requested_by");
        List<string> refs = NormalizeEvidenceRefs(evidenceRefs);

        DateTimeOffset? expiry = expiresAt?.ToUniversalTime();
        if (action == ResponseIntentAction.TemporaryPreStateDeny)
        {
            if (expiry is null
                || expiry == DateTimeOffset.MaxValue
                || expiry == DateTimeOffset.MinValue)
            {
                throw new DomainInvariantException(
                    $"{ResponseIntentCodes.TemporaryDenyRequiresExpiry}: TEMPORARY_PRE_STATE_DENY requires finite expires_at.");
            }
        }

        return new ResponseIntent(
            incidentId,
            nodeId,
            action,
            selector,
            expiry,
            urgency,
            refs,
            principal,
            idempotencyKey);
    }

    private static List<string> NormalizeEvidenceRefs(IEnumerable<string> evidenceRefs)
    {
        List<string> normalized = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string reference in evidenceRefs)
        {
            string trimmed = RequireNonEmpty(reference, "evidence_refs");
            if (!seen.Add(trimmed))
            {
                throw new DomainInvariantException("evidence_refs must be unique.");
            }

            normalized.Add(trimmed);
        }

        if (normalized.Count == 0)
        {
            throw new DomainInvariantException("evidence_refs must contain at least one reference.");
        }

        normalized.Sort(StringComparer.Ordinal);
        return normalized;
    }

    private static string RequireNonEmpty(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainInvariantException($"{label} is required.");
        }

        return value.Trim();
    }
}
