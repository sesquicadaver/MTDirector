using Mfc.Application.Abstractions.Integration;
using Mfc.Domain.Incident;

namespace Mfc.Application.Models;

/// <summary>One outbound RESPONSE_* feedback event (M7.4-05).</summary>
public sealed class ResponseFeedbackEventView
{
    public required Guid EventId { get; init; }

    public required string EventCode { get; init; }

    public required ResponseFeedbackEventKind Kind { get; init; }

    public required Guid IncidentId { get; init; }

    public required Guid NodeId { get; init; }

    public required IReadOnlyList<Guid> DeviceIds { get; init; }

    public byte[]? PolicyHash { get; init; }

    public byte[]? ArtifactHash { get; init; }

    public byte[]? PlanHash { get; init; }

    public string? VerificationResults { get; init; }

    public string? RollbackStatus { get; init; }

    public string? ResidualRisk { get; init; }

    public required Guid CorrelationId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public ResponseFeedbackDeliveryOutcome? DeliveryOutcome { get; init; }

    public static ResponseFeedbackEventView FromDomain(
        ResponseFeedbackEvent feedbackEvent,
        ResponseFeedbackDeliveryOutcome? deliveryOutcome)
        => new()
        {
            EventId = feedbackEvent.Id.Value,
            EventCode = feedbackEvent.EventCode,
            Kind = feedbackEvent.Kind,
            IncidentId = feedbackEvent.IncidentId.Value,
            NodeId = feedbackEvent.NodeId.Value,
            DeviceIds = feedbackEvent.DeviceIds.Select(static d => d.Value).ToArray(),
            PolicyHash = feedbackEvent.PolicyHash?.Bytes.ToArray(),
            ArtifactHash = feedbackEvent.ArtifactHash?.Bytes.ToArray(),
            PlanHash = feedbackEvent.PlanHash?.Bytes.ToArray(),
            VerificationResults = feedbackEvent.VerificationResults,
            RollbackStatus = feedbackEvent.RollbackStatus,
            ResidualRisk = feedbackEvent.ResidualRisk,
            CorrelationId = feedbackEvent.CorrelationId,
            CreatedAtUtc = feedbackEvent.CreatedAtUtc,
            DeliveryOutcome = deliveryOutcome,
        };
}
