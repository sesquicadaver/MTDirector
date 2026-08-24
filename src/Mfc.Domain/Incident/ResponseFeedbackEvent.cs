using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident.Primitives;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Incident;

/// <summary>
/// Immutable outbound feedback event for the external analytics complex (next-2 §Зворотний зв'язок / M7.4-05).
/// </summary>
public sealed class ResponseFeedbackEvent
{
    public ResponseFeedbackEventId Id { get; }

    public ResponseFeedbackEventKind Kind { get; }

    public string EventCode { get; }

    public IncidentId IncidentId { get; }

    public NodeId NodeId { get; }

    public IReadOnlyList<DeviceId> DeviceIds { get; }

    public Hash256? PolicyHash { get; }

    public Hash256? ArtifactHash { get; }

    public Hash256? PlanHash { get; }

    public string? VerificationResults { get; }

    public string? RollbackStatus { get; }

    public string? ResidualRisk { get; }

    public Guid CorrelationId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public bool Immutable { get; }

    private ResponseFeedbackEvent(
        ResponseFeedbackEventId id,
        ResponseFeedbackEventKind kind,
        string eventCode,
        IncidentId incidentId,
        NodeId nodeId,
        IReadOnlyList<DeviceId> deviceIds,
        Hash256? policyHash,
        Hash256? artifactHash,
        Hash256? planHash,
        string? verificationResults,
        string? rollbackStatus,
        string? residualRisk,
        Guid correlationId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Kind = kind;
        EventCode = eventCode;
        IncidentId = incidentId;
        NodeId = nodeId;
        DeviceIds = deviceIds;
        PolicyHash = policyHash;
        ArtifactHash = artifactHash;
        PlanHash = planHash;
        VerificationResults = verificationResults;
        RollbackStatus = rollbackStatus;
        ResidualRisk = residualRisk;
        CorrelationId = correlationId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Immutable = true;
    }

    public static ResponseFeedbackEvent Create(
        ResponseFeedbackEventKind kind,
        IncidentId incidentId,
        NodeId nodeId,
        IEnumerable<DeviceId> deviceIds,
        Guid correlationId,
        DateTimeOffset createdAtUtc,
        Hash256? policyHash = null,
        Hash256? artifactHash = null,
        Hash256? planHash = null,
        string? verificationResults = null,
        string? rollbackStatus = null,
        string? residualRisk = null,
        ResponseFeedbackEventId? id = null)
    {
        if (incidentId.Value == Guid.Empty)
        {
            throw new DomainInvariantException("incident_id must be a concrete UUID.");
        }

        if (nodeId.Value == Guid.Empty)
        {
            throw new DomainInvariantException("node_id must be a concrete UUID.");
        }

        if (correlationId == Guid.Empty)
        {
            throw new DomainInvariantException("correlation_id must be a concrete UUID.");
        }

        DeviceId[] devices = deviceIds?.ToArray() ?? [];
        return new ResponseFeedbackEvent(
            id ?? ResponseFeedbackEventId.New(),
            kind,
            ResponseFeedbackEventCodes.ForKind(kind),
            incidentId,
            nodeId,
            devices,
            policyHash,
            artifactHash,
            planHash,
            verificationResults,
            rollbackStatus,
            residualRisk,
            correlationId,
            createdAtUtc);
    }

    public static ResponseFeedbackEvent Reconstitute(
        ResponseFeedbackEventId id,
        ResponseFeedbackEventKind kind,
        string eventCode,
        IncidentId incidentId,
        NodeId nodeId,
        IReadOnlyList<DeviceId> deviceIds,
        Hash256? policyHash,
        Hash256? artifactHash,
        Hash256? planHash,
        string? verificationResults,
        string? rollbackStatus,
        string? residualRisk,
        Guid correlationId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventCode);
        return new ResponseFeedbackEvent(
            id,
            kind,
            eventCode,
            incidentId,
            nodeId,
            deviceIds,
            policyHash,
            artifactHash,
            planHash,
            verificationResults,
            rollbackStatus,
            residualRisk,
            correlationId,
            createdAtUtc);
    }
}
