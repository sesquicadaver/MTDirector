using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Incident;

/// <summary>
/// Binds a normalized <see cref="IncidentSignal"/> to a <see cref="ResponseAssessment"/> (M7.3-06 / next-2).
/// Pure contract; no persistence or routing writes.
/// </summary>
public static class IncidentResponseAssessmentContract
{
    public const string ContractVersion = "mfc.incident-response-assessment-contract.v1";

    /// <summary>Binds <paramref name="query"/> into a correlated response assessment.</summary>
    public static IncidentResponseAssessmentBinding Bind(IncidentResponseAssessmentQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Signal);

        FlowTuple correlationFlow = ResolveCorrelationFlow(query.Signal);
        IncidentId incidentId = MapIncidentId(query.Signal);
        ResponseAssessmentFeasibility feasibility = query.FeasibilityOverride
            ?? IncidentResponseFeasibilityClassifier.Classify(
                query.PacketPathClass,
                query.SessionVisibility,
                query.RouteTrace);

        ResponseAssessmentQualityInput qualityInput = new()
        {
            Feasibility = feasibility,
            SessionVisibility = query.SessionVisibility,
            RouteTrace = query.RouteTrace,
            PacketPathClass = query.PacketPathClass,
        };

        ResponseAssessment assessment = ResponseAssessment.CreateActive(
            incidentId,
            query.EndpointId,
            query.PresenceId,
            query.EnforcementNodeId,
            feasibility,
            query.AssessedAt,
            qualityInput: qualityInput);

        List<IncidentResponseAssessmentFinding> findings =
        [
            new()
            {
                Code = IncidentResponseAssessmentCodes.IncidentIdMappedFromEventId,
                Message = "Incident_id is bound to incident signal event_id.",
                Subject = incidentId.ToString(),
            },
        ];

        if (query.Signal.Confidence > assessment.Confidence)
        {
            findings.Add(new IncidentResponseAssessmentFinding
            {
                Code = IncidentResponseAssessmentCodes.SignalConfidenceExceedsAssessment,
                Message =
                    $"Signal confidence {query.Signal.Confidence} exceeds assessment confidence {assessment.Confidence} due to limited observability.",
                Subject = query.Signal.EventId.ToString(),
            });
        }

        findings.Add(new IncidentResponseAssessmentFinding
        {
            Code = IncidentResponseAssessmentCodes.ContractBound,
            Message = "Incident signal bound to response assessment.",
            Subject = assessment.AssessmentId.ToString(),
        });

        return new IncidentResponseAssessmentBinding
        {
            IncidentId = incidentId,
            CorrelationFlow = correlationFlow,
            Assessment = assessment,
            Findings = findings,
        };
    }

    /// <summary>Maps normalized signal identity to incident correlation identity (1:1 by event_id).</summary>
    public static IncidentId MapIncidentId(IncidentSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return new IncidentId(signal.EventId.Value);
    }

    /// <summary>Resolves the flow tuple used for session and path correlation.</summary>
    public static FlowTuple ResolveCorrelationFlow(IncidentSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        FlowTuple? flow = signal.OriginalFlow ?? signal.Flow;
        if (flow is null
            || string.IsNullOrWhiteSpace(flow.DestinationAddress)
            || string.IsNullOrWhiteSpace(flow.Protocol))
        {
            throw new DomainInvariantException(
                $"{IncidentResponseAssessmentCodes.MissingCorrelationFlow}: incident signal requires original_flow or flow with protocol and destination.");
        }

        return flow;
    }
}
