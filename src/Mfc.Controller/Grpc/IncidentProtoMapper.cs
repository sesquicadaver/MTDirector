using Google.Protobuf.WellKnownTypes;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using DomainEntityKind = Mfc.Domain.Incident.EntityReferenceKind;
using DomainEventId = Mfc.Domain.Incident.EventId;
using DomainFlow = Mfc.Domain.Incident.FlowTuple;
using DomainIndicator = Mfc.Domain.Incident.Indicator;
using DomainPacketPath = Mfc.Domain.Endpoint.ObservedPacketPathClass;
using DomainSessionVisibility = Mfc.Domain.Incident.SessionVisibilityStatus;
using DomainSeverity = Mfc.Domain.Incident.IncidentSeverity;
using DomainSignal = Mfc.Domain.Incident.IncidentSignal;
using DomainSourceType = Mfc.Domain.Incident.IncidentSignalSourceType;
using Enum = System.Enum;
using ProtoBinding = Mfc.Contracts.Mfc.V1.IncidentResponseAssessmentBinding;
using ProtoFinding = Mfc.Contracts.Mfc.V1.IncidentResponseAssessmentFinding;
using ProtoPacketPath = Mfc.Contracts.Mfc.V1.ObservedPacketPathClass;
using ProtoResponseAssessment = Mfc.Contracts.Mfc.V1.ResponseAssessment;
using ProtoSeverity = Mfc.Contracts.Mfc.V1.IncidentSeverity;
using ProtoSignal = Mfc.Contracts.Mfc.V1.IncidentSignal;
using ProtoSourceType = Mfc.Contracts.Mfc.V1.IncidentSignalSourceType;

namespace Mfc.Controller.Grpc;

/// <summary>Maps Incident Application views / commands ↔ Contracts (SEC-06).</summary>
internal static class IncidentProtoMapper
{
    public static ProtoSignal ToProto(IncidentSignalView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ProtoSignal message = new()
        {
            EventId = ProtoUuid.FromGuid(view.EventId),
            SourceEventId = view.SourceEventId,
            OccurredAt = Timestamp.FromDateTimeOffset(view.OccurredAt),
            ReceivedAt = Timestamp.FromDateTimeOffset(view.ReceivedAt),
            SourceType = ToProtoSourceType(view.SourceType),
            Category = view.Category,
            Severity = ToProtoSeverity(view.Severity),
            Confidence = view.Confidence,
            DeduplicationKey = view.DeduplicationKey,
        };

        if (view.SiteId is Guid siteId)
        {
            message.SiteId = ProtoUuid.FromGuid(siteId);
        }

        if (view.NodeId is Guid nodeId)
        {
            message.NodeId = ProtoUuid.FromGuid(nodeId);
        }

        if (view.DeviceId is Guid deviceId)
        {
            message.DeviceId = ProtoUuid.FromGuid(deviceId);
        }

        message.Entities.AddRange(view.Entities.Select(ToProtoEntity));
        if (view.Flow is not null)
        {
            message.Flow = ToProtoFlow(view.Flow);
        }

        if (view.OriginalFlow is not null)
        {
            message.OriginalFlow = ToProtoFlow(view.OriginalFlow);
        }

        if (view.TranslatedFlow is not null)
        {
            message.TranslatedFlow = ToProtoFlow(view.TranslatedFlow);
        }

        if (view.VlanId is ushort vlan)
        {
            message.VlanId = vlan;
        }

        if (view.Interface is not null)
        {
            message.Interface = view.Interface;
        }

        if (view.Vrf is not null)
        {
            message.Vrf = view.Vrf;
        }

        if (view.ContainerId is not null)
        {
            message.ContainerId = view.ContainerId;
        }

        if (view.VpnIdentity is not null)
        {
            message.VpnIdentity = view.VpnIdentity;
        }

        message.Indicators.AddRange(view.Indicators.Select(static i => new IncidentIndicator
        {
            Type = i.Type,
            Value = i.Value,
        }));
        message.EvidenceRefs.AddRange(view.EvidenceRefs);
        if (view.RawEventRef is not null)
        {
            message.RawEventRef = view.RawEventRef;
        }

        return message;
    }

    public static ProtoBinding ToProto(IncidentResponseAssessmentBindingView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ProtoBinding message = new()
        {
            IncidentId = ProtoUuid.FromGuid(view.IncidentId),
            CorrelationFlow = ToProtoFlow(view.CorrelationFlow),
            Assessment = ToProtoAssessment(view.Assessment),
        };
        message.Findings.AddRange(view.Findings.Select(static f =>
        {
            ProtoFinding finding = new()
            {
                Code = f.Code,
                Message = f.Message,
            };
            if (f.Subject is not null)
            {
                finding.Subject = f.Subject;
            }

            return finding;
        }));
        return message;
    }

    public static IngestIncidentSignalCommand ToIngestCommand(IngestIncidentSignalRequest request, string actor)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        return new IngestIncidentSignalCommand
        {
            Actor = actor,
            EventId = ProtoUuid.ToGuid(request.EventId),
            SourceEventId = request.SourceEventId,
            OccurredAt = request.OccurredAt?.ToDateTimeOffset()
                ?? throw new ArgumentException("occurred_at is required."),
            ReceivedAt = request.ReceivedAt?.ToDateTimeOffset()
                ?? throw new ArgumentException("received_at is required."),
            SourceType = ToDomainSourceType(request.SourceType),
            Category = request.Category,
            Severity = ToDomainSeverity(request.Severity),
            Confidence = request.Confidence,
            DeduplicationKey = request.DeduplicationKey,
            SiteId = ProtoUuid.ToNullableGuid(request.SiteId),
            NodeId = ProtoUuid.ToNullableGuid(request.NodeId),
            DeviceId = ProtoUuid.ToNullableGuid(request.DeviceId),
            Entities = request.Entities.Select(ToDomainEntity).ToArray(),
            Flow = ToDomainFlow(request.Flow),
            OriginalFlow = ToDomainFlow(request.OriginalFlow),
            TranslatedFlow = ToDomainFlow(request.TranslatedFlow),
            VlanId = request.HasVlanId ? checked((ushort)request.VlanId) : null,
            Interface = request.HasInterface ? request.Interface : null,
            Vrf = request.HasVrf ? request.Vrf : null,
            ContainerId = request.HasContainerId ? request.ContainerId : null,
            VpnIdentity = request.HasVpnIdentity ? request.VpnIdentity : null,
            Indicators = request.Indicators
                .Select(static i => DomainIndicator.Create(i.Type, i.Value))
                .ToArray(),
            EvidenceRefs = request.EvidenceRefs.ToArray(),
            RawEventRef = request.HasRawEventRef ? request.RawEventRef : null,
            ForbiddenIngressFieldNames = request.ForbiddenIngressFieldNames.Count == 0
                ? null
                : request.ForbiddenIngressFieldNames.ToArray(),
        };
    }

    public static BindIncidentResponseAssessmentCommand ToBindCommand(
        BindIncidentResponseAssessmentRequest request,
        string actor)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (request.Signal is null)
        {
            throw new ArgumentException("signal is required.");
        }

        IngestIncidentSignalCommand ingest = ToIngestCommand(request.Signal, actor);
        DomainSignal signal = DomainSignal.Create(
            new DomainEventId(ingest.EventId),
            ingest.SourceEventId,
            ingest.OccurredAt,
            ingest.ReceivedAt,
            ingest.SourceType,
            ingest.Category,
            ingest.Severity,
            ingest.Confidence,
            ingest.DeduplicationKey,
            ingest.Entities,
            ingest.Flow,
            ingest.OriginalFlow,
            ingest.TranslatedFlow,
            ingest.SiteId is Guid siteId ? new SiteId(siteId) : null,
            ingest.NodeId is Guid nodeId ? new NodeId(nodeId) : null,
            ingest.DeviceId is Guid deviceId ? new DeviceId(deviceId) : null,
            ingest.VlanId,
            ingest.Interface,
            ingest.Vrf,
            ingest.ContainerId,
            ingest.VpnIdentity,
            ingest.Indicators,
            ingest.EvidenceRefs,
            ingest.RawEventRef);

        return new BindIncidentResponseAssessmentCommand
        {
            Actor = actor,
            Signal = signal,
            EndpointId = ProtoUuid.ToGuid(request.EndpointId),
            PresenceId = ProtoUuid.ToGuid(request.PresenceId),
            EnforcementNodeId = ProtoUuid.ToGuid(request.EnforcementNodeId),
            AssessedAt = request.AssessedAt?.ToDateTimeOffset()
                ?? throw new ArgumentException("assessed_at is required."),
            SessionVisibility = request.HasSessionVisibility
                ? ToDomainSessionVisibility(request.SessionVisibility)
                : null,
            PacketPathClass = ToDomainPacketPath(request.PacketPathClass),
        };
    }

    private static ProtoResponseAssessment ToProtoAssessment(ResponseAssessmentView view)
    {
        ProtoResponseAssessment message = new()
        {
            AssessmentId = ProtoUuid.FromGuid(view.AssessmentId),
            IncidentId = ProtoUuid.FromGuid(view.IncidentId),
            EndpointId = ProtoUuid.FromGuid(view.EndpointId),
            PresenceId = ProtoUuid.FromGuid(view.PresenceId),
            EnforcementNodeId = ProtoUuid.FromGuid(view.EnforcementNodeId),
            Feasibility = view.Feasibility,
            VisibilityStatus = view.VisibilityStatus,
            Confidence = view.Confidence,
            Status = view.Status,
            CreatedAt = Timestamp.FromDateTimeOffset(view.CreatedAtUtc),
        };
        if (view.InvalidatedAtUtc is DateTimeOffset invalidated)
        {
            message.InvalidatedAt = Timestamp.FromDateTimeOffset(invalidated);
        }

        if (view.InvalidationReason is not null)
        {
            message.InvalidationReason = view.InvalidationReason;
        }

        return message;
    }

    private static IncidentEntityReference ToProtoEntity(EntityReferenceView view) =>
        new()
        {
            Kind = Enum.TryParse(view.Kind, ignoreCase: false, out DomainEntityKind kind)
                ? ToProtoEntityKind(kind)
                : IncidentEntityReferenceKind.Unspecified,
            Value = view.Value,
        };

    private static IncidentFlowTuple ToProtoFlow(FlowTupleView view)
    {
        IncidentFlowTuple flow = new();
        if (view.SourceAddress is not null)
        {
            flow.SourceAddress = view.SourceAddress;
        }

        if (view.SourcePort is ushort sourcePort)
        {
            flow.SourcePort = sourcePort;
        }

        if (view.DestinationAddress is not null)
        {
            flow.DestinationAddress = view.DestinationAddress;
        }

        if (view.DestinationPort is ushort destinationPort)
        {
            flow.DestinationPort = destinationPort;
        }

        if (view.Protocol is not null)
        {
            flow.Protocol = view.Protocol;
        }

        return flow;
    }

    private static EntityReference ToDomainEntity(IncidentEntityReference entity) =>
        EntityReference.Create(ToDomainEntityKind(entity.Kind), entity.Value);

    private static DomainFlow? ToDomainFlow(IncidentFlowTuple? flow)
    {
        if (flow is null)
        {
            return null;
        }

        bool empty = !flow.HasSourceAddress
            && !flow.HasDestinationAddress
            && !flow.HasProtocol
            && !flow.HasSourcePort
            && !flow.HasDestinationPort;
        if (empty)
        {
            return null;
        }

        return DomainFlow.Create(
            flow.HasSourceAddress ? flow.SourceAddress : null,
            flow.HasSourcePort ? checked((ushort)flow.SourcePort) : null,
            flow.HasDestinationAddress ? flow.DestinationAddress : null,
            flow.HasDestinationPort ? checked((ushort)flow.DestinationPort) : null,
            flow.HasProtocol ? flow.Protocol : null);
    }

    private static DomainSourceType ToDomainSourceType(ProtoSourceType value) =>
        value switch
        {
            ProtoSourceType.Siem => DomainSourceType.Siem,
            ProtoSourceType.Ndr => DomainSourceType.Ndr,
            ProtoSourceType.Edr => DomainSourceType.Edr,
            ProtoSourceType.Ids => DomainSourceType.Ids,
            ProtoSourceType.RouterOsLog => DomainSourceType.RouterOsLog,
            ProtoSourceType.FlowAnalyzer => DomainSourceType.FlowAnalyzer,
            ProtoSourceType.Monitoring => DomainSourceType.Monitoring,
            _ => throw new ArgumentException($"Unsupported incident signal source_type '{value}'."),
        };

    private static ProtoSourceType ToProtoSourceType(string value) =>
        Enum.TryParse(value, ignoreCase: false, out DomainSourceType parsed)
            ? parsed switch
            {
                DomainSourceType.Siem => ProtoSourceType.Siem,
                DomainSourceType.Ndr => ProtoSourceType.Ndr,
                DomainSourceType.Edr => ProtoSourceType.Edr,
                DomainSourceType.Ids => ProtoSourceType.Ids,
                DomainSourceType.RouterOsLog => ProtoSourceType.RouterOsLog,
                DomainSourceType.FlowAnalyzer => ProtoSourceType.FlowAnalyzer,
                DomainSourceType.Monitoring => ProtoSourceType.Monitoring,
                _ => ProtoSourceType.Unspecified,
            }
            : ProtoSourceType.Unspecified;

    private static DomainSeverity ToDomainSeverity(ProtoSeverity value) =>
        value switch
        {
            ProtoSeverity.Info => DomainSeverity.Info,
            ProtoSeverity.Low => DomainSeverity.Low,
            ProtoSeverity.Medium => DomainSeverity.Medium,
            ProtoSeverity.High => DomainSeverity.High,
            ProtoSeverity.Critical => DomainSeverity.Critical,
            _ => throw new ArgumentException($"Unsupported incident severity '{value}'."),
        };

    private static ProtoSeverity ToProtoSeverity(string value) =>
        Enum.TryParse(value, ignoreCase: false, out DomainSeverity parsed)
            ? parsed switch
            {
                DomainSeverity.Info => ProtoSeverity.Info,
                DomainSeverity.Low => ProtoSeverity.Low,
                DomainSeverity.Medium => ProtoSeverity.Medium,
                DomainSeverity.High => ProtoSeverity.High,
                DomainSeverity.Critical => ProtoSeverity.Critical,
                _ => ProtoSeverity.Unspecified,
            }
            : ProtoSeverity.Unspecified;

    private static DomainEntityKind ToDomainEntityKind(IncidentEntityReferenceKind value) =>
        value switch
        {
            IncidentEntityReferenceKind.IpAddress => DomainEntityKind.IpAddress,
            IncidentEntityReferenceKind.MacAddress => DomainEntityKind.MacAddress,
            IncidentEntityReferenceKind.Hostname => DomainEntityKind.Hostname,
            IncidentEntityReferenceKind.User => DomainEntityKind.User,
            IncidentEntityReferenceKind.Domain => DomainEntityKind.Domain,
            IncidentEntityReferenceKind.Url => DomainEntityKind.Url,
            IncidentEntityReferenceKind.Hash => DomainEntityKind.Hash,
            IncidentEntityReferenceKind.Other => DomainEntityKind.Other,
            _ => throw new ArgumentException($"Unsupported entity kind '{value}'."),
        };

    private static IncidentEntityReferenceKind ToProtoEntityKind(DomainEntityKind value) =>
        value switch
        {
            DomainEntityKind.IpAddress => IncidentEntityReferenceKind.IpAddress,
            DomainEntityKind.MacAddress => IncidentEntityReferenceKind.MacAddress,
            DomainEntityKind.Hostname => IncidentEntityReferenceKind.Hostname,
            DomainEntityKind.User => IncidentEntityReferenceKind.User,
            DomainEntityKind.Domain => IncidentEntityReferenceKind.Domain,
            DomainEntityKind.Url => IncidentEntityReferenceKind.Url,
            DomainEntityKind.Hash => IncidentEntityReferenceKind.Hash,
            DomainEntityKind.Other => IncidentEntityReferenceKind.Other,
            _ => IncidentEntityReferenceKind.Unspecified,
        };

    private static DomainSessionVisibility ToDomainSessionVisibility(IncidentSessionVisibilityStatus value) =>
        value switch
        {
            IncidentSessionVisibilityStatus.Full => DomainSessionVisibility.Full,
            IncidentSessionVisibilityStatus.Partial => DomainSessionVisibility.Partial,
            IncidentSessionVisibilityStatus.NotObserved => DomainSessionVisibility.NotObserved,
            _ => throw new ArgumentException($"Unsupported session_visibility '{value}'."),
        };

    private static DomainPacketPath ToDomainPacketPath(ProtoPacketPath value) =>
        value switch
        {
            ProtoPacketPath.Unspecified => DomainPacketPath.Unknown,
            ProtoPacketPath.Unknown => DomainPacketPath.Unknown,
            ProtoPacketPath.CpuFirewall => DomainPacketPath.CpuFirewall,
            ProtoPacketPath.HardwareOffloaded => DomainPacketPath.HardwareOffloaded,
            ProtoPacketPath.Mixed => DomainPacketPath.Mixed,
            ProtoPacketPath.Indeterminate => DomainPacketPath.Indeterminate,
            _ => DomainPacketPath.Unknown,
        };
}
