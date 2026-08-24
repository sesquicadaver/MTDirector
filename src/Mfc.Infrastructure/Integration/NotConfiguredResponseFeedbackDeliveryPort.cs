using Mfc.Application.Abstractions.Integration;
using Mfc.Domain.Incident;

namespace Mfc.Infrastructure.Integration;

/// <summary>
/// Default delivery port when no external analytics webhook is configured (M7.4-05).
/// Events remain persisted and queryable via <see cref="Abstractions.Persistence.IResponseFeedbackEventStore"/>.
/// </summary>
public sealed class NotConfiguredResponseFeedbackDeliveryPort : IResponseFeedbackDeliveryPort
{
    public Task<ResponseFeedbackDeliveryResult> DeliverAsync(
        ResponseFeedbackEvent feedbackEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedbackEvent);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ResponseFeedbackDeliveryResult
        {
            Outcome = ResponseFeedbackDeliveryOutcome.NotConfigured,
        });
    }
}
