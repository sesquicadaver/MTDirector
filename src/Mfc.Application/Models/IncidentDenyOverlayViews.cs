using Mfc.Domain.Endpoint;
using Mfc.Domain.Policy;

namespace Mfc.Application.Models;

public sealed class IncidentDenyOverlayValidationView
{
    public required string ValidationCode { get; init; }

    public required Guid IncidentId { get; init; }

    public required Guid NodeId { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required string Reason { get; init; }

    public required IReadOnlyList<string> EvidenceRefs { get; init; }

    public static IncidentDenyOverlayValidationView FromDomain(
        string validationCode,
        IncidentDenyOverlayMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new IncidentDenyOverlayValidationView
        {
            ValidationCode = validationCode,
            IncidentId = metadata.IncidentId.Value,
            NodeId = metadata.NodeId,
            ExpiresAt = metadata.ExpiresAt,
            Reason = metadata.Reason,
            EvidenceRefs = metadata.EvidenceRefs,
        };
    }
}
