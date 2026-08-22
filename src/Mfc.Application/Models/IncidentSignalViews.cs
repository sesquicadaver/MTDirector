using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Models;

public sealed class EntityReferenceView
{
    public required string Kind { get; init; }

    public required string Value { get; init; }

    public static EntityReferenceView FromDomain(EntityReference entity) =>
        new()
        {
            Kind = entity.Kind.ToString(),
            Value = entity.Value,
        };
}

public sealed class FlowTupleView
{
    public string? SourceAddress { get; init; }

    public ushort? SourcePort { get; init; }

    public string? DestinationAddress { get; init; }

    public ushort? DestinationPort { get; init; }

    public string? Protocol { get; init; }

    public static FlowTupleView? FromDomain(FlowTuple? flow) =>
        flow is null
            ? null
            : new FlowTupleView
            {
                SourceAddress = flow.SourceAddress,
                SourcePort = flow.SourcePort,
                DestinationAddress = flow.DestinationAddress,
                DestinationPort = flow.DestinationPort,
                Protocol = flow.Protocol,
            };
}

public sealed class IndicatorView
{
    public required string Type { get; init; }

    public required string Value { get; init; }

    public static IndicatorView FromDomain(Indicator indicator) =>
        new()
        {
            Type = indicator.Type,
            Value = indicator.Value,
        };
}

/// <summary>Normalized incident signal returned after ingress validation (M7.3-01).</summary>
public sealed class IncidentSignalView
{
    public required Guid EventId { get; init; }

    public required string SourceEventId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required string SourceType { get; init; }

    public required string Category { get; init; }

    public required string Severity { get; init; }

    public required int Confidence { get; init; }

    public Guid? SiteId { get; init; }

    public Guid? NodeId { get; init; }

    public Guid? DeviceId { get; init; }

    public required IReadOnlyList<EntityReferenceView> Entities { get; init; }

    public FlowTupleView? Flow { get; init; }

    public FlowTupleView? OriginalFlow { get; init; }

    public FlowTupleView? TranslatedFlow { get; init; }

    public ushort? VlanId { get; init; }

    public string? Interface { get; init; }

    public string? Vrf { get; init; }

    public string? ContainerId { get; init; }

    public string? VpnIdentity { get; init; }

    public required IReadOnlyList<IndicatorView> Indicators { get; init; }

    public required IReadOnlyList<string> EvidenceRefs { get; init; }

    public required string DeduplicationKey { get; init; }

    public string? RawEventRef { get; init; }

    public static IncidentSignalView FromDomain(IncidentSignal signal) =>
        new()
        {
            EventId = signal.EventId.Value,
            SourceEventId = signal.SourceEventId,
            OccurredAt = signal.OccurredAt,
            ReceivedAt = signal.ReceivedAt,
            SourceType = signal.SourceType.ToString(),
            Category = signal.Category,
            Severity = signal.Severity.ToString(),
            Confidence = signal.Confidence,
            SiteId = signal.SiteId?.Value,
            NodeId = signal.NodeId?.Value,
            DeviceId = signal.DeviceId?.Value,
            Entities = signal.Entities.Select(EntityReferenceView.FromDomain).ToArray(),
            Flow = FlowTupleView.FromDomain(signal.Flow),
            OriginalFlow = FlowTupleView.FromDomain(signal.OriginalFlow),
            TranslatedFlow = FlowTupleView.FromDomain(signal.TranslatedFlow),
            VlanId = signal.VlanId,
            Interface = signal.Interface,
            Vrf = signal.Vrf,
            ContainerId = signal.ContainerId,
            VpnIdentity = signal.VpnIdentity,
            Indicators = signal.Indicators.Select(IndicatorView.FromDomain).ToArray(),
            EvidenceRefs = signal.EvidenceRefs.ToArray(),
            DeduplicationKey = signal.DeduplicationKey,
            RawEventRef = signal.RawEventRef,
        };
}
