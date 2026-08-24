using Mfc.Domain.Incident;

namespace Mfc.Application.Abstractions.Integration;

/// <summary>Delivery outcome for outbound RESPONSE_* feedback events.</summary>
public enum ResponseFeedbackDeliveryOutcome
{
    Delivered = 1,
    NotConfigured = 2,
}

/// <summary>Result of delivering one feedback event to the external analytics complex.</summary>
public sealed class ResponseFeedbackDeliveryResult
{
    public required ResponseFeedbackDeliveryOutcome Outcome { get; init; }
}

/// <summary>
/// Delivers RESPONSE_* feedback events to the external analytics complex (M7.4-05).
/// Default implementation is not configured; events remain queryable from the store.
/// </summary>
public interface IResponseFeedbackDeliveryPort
{
    Task<ResponseFeedbackDeliveryResult> DeliverAsync(
        ResponseFeedbackEvent feedbackEvent,
        CancellationToken cancellationToken = default);
}
