using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Incident;

/// <summary>
/// Normalized incident signal ingress contract (M7.3-01 / next-2 §IncidentSignal).
/// Controller does not persist raw syslog; only validated normalized events pass ingress.
/// </summary>
public sealed class IncidentSignal : IEquatable<IncidentSignal>
{
    public EventId EventId { get; }

    public string SourceEventId { get; }

    public DateTimeOffset OccurredAt { get; }

    public DateTimeOffset ReceivedAt { get; }

    public IncidentSignalSourceType SourceType { get; }

    public string Category { get; }

    public IncidentSeverity Severity { get; }

    public int Confidence { get; }

    public SiteId? SiteId { get; }

    public NodeId? NodeId { get; }

    public DeviceId? DeviceId { get; }

    public IReadOnlyList<EntityReference> Entities { get; }

    public FlowTuple? Flow { get; }

    public FlowTuple? OriginalFlow { get; }

    public FlowTuple? TranslatedFlow { get; }

    public ushort? VlanId { get; }

    public string? Interface { get; }

    public string? Vrf { get; }

    public string? ContainerId { get; }

    public string? VpnIdentity { get; }

    public IReadOnlyList<Indicator> Indicators { get; }

    public IReadOnlyList<string> EvidenceRefs { get; }

    public string DeduplicationKey { get; }

    public string? RawEventRef { get; }

    private IncidentSignal(
        EventId eventId,
        string sourceEventId,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt,
        IncidentSignalSourceType sourceType,
        string category,
        IncidentSeverity severity,
        int confidence,
        SiteId? siteId,
        NodeId? nodeId,
        DeviceId? deviceId,
        IReadOnlyList<EntityReference> entities,
        FlowTuple? flow,
        FlowTuple? originalFlow,
        FlowTuple? translatedFlow,
        ushort? vlanId,
        string? interfaceName,
        string? vrf,
        string? containerId,
        string? vpnIdentity,
        IReadOnlyList<Indicator> indicators,
        IReadOnlyList<string> evidenceRefs,
        string deduplicationKey,
        string? rawEventRef)
    {
        EventId = eventId;
        SourceEventId = sourceEventId;
        OccurredAt = occurredAt;
        ReceivedAt = receivedAt;
        SourceType = sourceType;
        Category = category;
        Severity = severity;
        Confidence = confidence;
        SiteId = siteId;
        NodeId = nodeId;
        DeviceId = deviceId;
        Entities = entities;
        Flow = flow;
        OriginalFlow = originalFlow;
        TranslatedFlow = translatedFlow;
        VlanId = vlanId;
        Interface = interfaceName;
        Vrf = vrf;
        ContainerId = containerId;
        VpnIdentity = vpnIdentity;
        Indicators = indicators;
        EvidenceRefs = evidenceRefs;
        DeduplicationKey = deduplicationKey;
        RawEventRef = rawEventRef;
    }

    /// <summary>Creates a validated normalized incident signal for ingress.</summary>
    public static IncidentSignal Create(
        EventId eventId,
        string sourceEventId,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt,
        IncidentSignalSourceType sourceType,
        string category,
        IncidentSeverity severity,
        int confidence,
        string deduplicationKey,
        IReadOnlyList<EntityReference>? entities = null,
        FlowTuple? flow = null,
        FlowTuple? originalFlow = null,
        FlowTuple? translatedFlow = null,
        SiteId? siteId = null,
        NodeId? nodeId = null,
        DeviceId? deviceId = null,
        ushort? vlanId = null,
        string? interfaceName = null,
        string? vrf = null,
        string? containerId = null,
        string? vpnIdentity = null,
        IReadOnlyList<Indicator>? indicators = null,
        IReadOnlyList<string>? evidenceRefs = null,
        string? rawEventRef = null)
    {
        if (eventId.Value == Guid.Empty)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.MissingEventId}: event_id must be a non-empty UUID.");
        }

        string normalizedSourceEventId = RequireNonEmpty(sourceEventId, IncidentSignalCodes.MissingSourceEventId, "source_event_id");
        if (normalizedSourceEventId.Length > 256)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.FieldTooLong}: source_event_id exceeds 256 characters.");
        }

        if (!Enum.IsDefined(sourceType))
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.InvalidSourceType}: source_type '{sourceType}' is not supported.");
        }

        if (!Enum.IsDefined(severity))
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.InvalidSeverity}: severity '{severity}' is not supported.");
        }

        if (confidence is < 0 or > 100)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.InvalidConfidence}: confidence must be between 0 and 100.");
        }

        DateTimeOffset normalizedOccurredAt = NormalizeInstant(occurredAt, IncidentSignalCodes.InvalidOccurredAt, "occurred_at");
        DateTimeOffset normalizedReceivedAt = NormalizeInstant(receivedAt, IncidentSignalCodes.InvalidReceivedAt, "received_at");
        if (normalizedReceivedAt < normalizedOccurredAt)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.ReceivedBeforeOccurred}: received_at must not precede occurred_at.");
        }

        string normalizedCategory = RequireNonEmpty(category, IncidentSignalCodes.MissingCategory, "category");
        if (normalizedCategory.Length > 256)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.FieldTooLong}: category exceeds 256 characters.");
        }

        string normalizedDeduplicationKey = RequireNonEmpty(
            deduplicationKey,
            IncidentSignalCodes.MissingDeduplicationKey,
            "deduplication_key");
        if (normalizedDeduplicationKey.Length > 512)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.FieldTooLong}: deduplication_key exceeds 512 characters.");
        }

        IReadOnlyList<EntityReference> normalizedEntities = NormalizeList(entities);
        IReadOnlyList<Indicator> normalizedIndicators = NormalizeList(indicators);
        IReadOnlyList<string> normalizedEvidenceRefs = NormalizeEvidenceRefs(evidenceRefs);
        string? normalizedRawEventRef = NormalizeOptionalRef(rawEventRef, 2048, "raw_event_ref");

        IncidentSignalIngressGuard.EnsureRouterOsLogCategory(sourceType, normalizedCategory);
        IncidentSignalIngressGuard.RejectInlineRawSyslog(normalizedRawEventRef, normalizedEvidenceRefs);

        if (vlanId is < 1 or > 4094)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.InvalidVlanId}: vlan_id must be between 1 and 4094.");
        }

        return new IncidentSignal(
            eventId,
            normalizedSourceEventId,
            normalizedOccurredAt,
            normalizedReceivedAt,
            sourceType,
            normalizedCategory,
            severity,
            confidence,
            siteId,
            nodeId,
            deviceId,
            normalizedEntities,
            flow,
            originalFlow,
            translatedFlow,
            vlanId,
            NormalizeOptionalRef(interfaceName, 128, "interface"),
            NormalizeOptionalRef(vrf, 128, "vrf"),
            NormalizeOptionalRef(containerId, 128, "container_id"),
            NormalizeOptionalRef(vpnIdentity, 256, "vpn_identity"),
            normalizedIndicators,
            normalizedEvidenceRefs,
            normalizedDeduplicationKey,
            normalizedRawEventRef);
    }

    public bool Equals(IncidentSignal? other) =>
        other is not null
        && EventId.Equals(other.EventId)
        && SourceEventId == other.SourceEventId
        && OccurredAt.Equals(other.OccurredAt)
        && ReceivedAt.Equals(other.ReceivedAt)
        && SourceType == other.SourceType
        && Category == other.Category
        && Severity == other.Severity
        && Confidence == other.Confidence
        && Nullable.Equals(SiteId, other.SiteId)
        && Nullable.Equals(NodeId, other.NodeId)
        && Nullable.Equals(DeviceId, other.DeviceId)
        && Entities.SequenceEqual(other.Entities)
        && Equals(Flow, other.Flow)
        && Equals(OriginalFlow, other.OriginalFlow)
        && Equals(TranslatedFlow, other.TranslatedFlow)
        && VlanId == other.VlanId
        && Interface == other.Interface
        && Vrf == other.Vrf
        && ContainerId == other.ContainerId
        && VpnIdentity == other.VpnIdentity
        && Indicators.SequenceEqual(other.Indicators)
        && EvidenceRefs.SequenceEqual(other.EvidenceRefs)
        && DeduplicationKey == other.DeduplicationKey
        && RawEventRef == other.RawEventRef;

    public override bool Equals(object? obj) => obj is IncidentSignal other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(EventId);
        hash.Add(SourceEventId);
        hash.Add(OccurredAt);
        hash.Add(ReceivedAt);
        hash.Add(SourceType);
        hash.Add(Category);
        hash.Add(Severity);
        hash.Add(Confidence);
        hash.Add(SiteId);
        hash.Add(NodeId);
        hash.Add(DeviceId);
        foreach (EntityReference entity in Entities)
        {
            hash.Add(entity);
        }

        hash.Add(Flow);
        hash.Add(OriginalFlow);
        hash.Add(TranslatedFlow);
        hash.Add(VlanId);
        hash.Add(Interface);
        hash.Add(Vrf);
        hash.Add(ContainerId);
        hash.Add(VpnIdentity);
        foreach (Indicator indicator in Indicators)
        {
            hash.Add(indicator);
        }

        foreach (string evidenceRef in EvidenceRefs)
        {
            hash.Add(evidenceRef);
        }

        hash.Add(DeduplicationKey);
        hash.Add(RawEventRef);
        return hash.ToHashCode();
    }

    private static string RequireNonEmpty(string value, string code, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainInvariantException($"{code}: {fieldName} is required.");
        }

        return value.Trim();
    }

    private static DateTimeOffset NormalizeInstant(DateTimeOffset value, string code, string fieldName)
    {
        if (value == default)
        {
            throw new DomainInvariantException($"{code}: {fieldName} must be a valid UTC instant.");
        }

        return value.ToUniversalTime();
    }

    private static T[] NormalizeList<T>(IReadOnlyList<T>? values)
        => values is null || values.Count == 0 ? Array.Empty<T>() : values.ToArray();

    private static string[] NormalizeEvidenceRefs(IReadOnlyList<string>? evidenceRefs)
    {
        if (evidenceRefs is null || evidenceRefs.Count == 0)
        {
            return Array.Empty<string>();
        }

        string[] normalized = new string[evidenceRefs.Count];
        for (int i = 0; i < evidenceRefs.Count; i++)
        {
            string? evidenceRef = evidenceRefs[i];
            if (string.IsNullOrWhiteSpace(evidenceRef))
            {
                throw new DomainInvariantException(
                    $"{IncidentSignalCodes.FieldTooLong}: evidence_refs entries must be non-empty.");
            }

            string trimmed = evidenceRef.Trim();
            if (trimmed.Length > 512)
            {
                throw new DomainInvariantException(
                    $"{IncidentSignalCodes.FieldTooLong}: evidence_refs entry exceeds 512 characters.");
            }

            normalized[i] = trimmed;
        }

        return normalized;
    }

    private static string? NormalizeOptionalRef(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.FieldTooLong}: {fieldName} exceeds {maxLength} characters.");
        }

        return trimmed;
    }
}
