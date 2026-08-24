using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Incident.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Append-only store for immutable RESPONSE_* feedback events (M7.4-05).</summary>
public interface IResponseFeedbackEventStore
{
    Task AppendAsync(ResponseFeedbackEvent feedbackEvent, CancellationToken cancellationToken = default);

    Task<ResponseFeedbackEvent?> GetAsync(
        ResponseFeedbackEventId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResponseFeedbackEvent>> ListByIncidentAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResponseFeedbackEvent>> ListByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default);
}
