using Mfc.Domain.Endpoint;

namespace Mfc.Domain.Incident;

/// <summary>
/// Normative ResponseIntent → ResponseAssessment feasibility matrix (next-2 §ResponseAssessment table / M7.4-02).
/// </summary>
public static class ResponseIntentFeasibilityMatrix
{
    /// <summary>Classifies enforceability for one response intent and scripted observation inputs.</summary>
    public static ResponseIntentFeasibilityResult Assess(ResponseIntentFeasibilityQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Intent);

        List<ResponseIntentFeasibilityFinding> findings = [];

        if (query.Intent.Action is ResponseIntentAction.RevokeTemporaryException
            or ResponseIntentAction.RestoreCommittedPolicy)
        {
            findings.Add(new ResponseIntentFeasibilityFinding
            {
                Code = ResponseIntentCodes.NonDenyActionFullyEnforceable,
                Message = $"{query.Intent.Action} does not require IP-filter enforcement on live sessions.",
            });
            return Finish(ResponseAssessmentFeasibility.FullyEnforceable, findings);
        }

        if (query.L2BridgeVlanBypass)
        {
            findings.Add(new ResponseIntentFeasibilityFinding
            {
                Code = ResponseIntentCodes.L2BridgeVlanNotEnforceable,
                Message = "L2 bridge/VLAN traffic bypasses IP firewall enforcement.",
            });
            return Finish(ResponseAssessmentFeasibility.NotEnforceableByIpFilter, findings);
        }

        if (query.ProvenRoutedContainerForward
            && query.PacketPathClass == ObservedPacketPathClass.CpuFirewall)
        {
            findings.Add(new ResponseIntentFeasibilityFinding
            {
                Code = ResponseIntentCodes.ContainerForwardProven,
                Message = "Routed container VETH/FORWARD path is proven through CPU firewall.",
            });
            return Finish(ResponseAssessmentFeasibility.FullyEnforceable, findings);
        }

        if (query.FastTrackSessionActive)
        {
            findings.Add(new ResponseIntentFeasibilityFinding
            {
                Code = ResponseIntentCodes.FastTrackLimitsToNewConnections,
                Message = "Existing FastTrack session cannot be fully terminated by IP filter alone.",
            });
            return Finish(ResponseAssessmentFeasibility.NewConnectionsOnly, findings);
        }

        ResponseAssessmentFeasibility feasibility = IncidentResponseFeasibilityClassifier.Classify(
            query.PacketPathClass,
            query.SessionVisibility,
            query.RouteTrace);
        findings.Add(new ResponseIntentFeasibilityFinding
        {
            Code = ResponseIntentCodes.MatrixClassified,
            Message = $"Feasibility classified as {feasibility} from packet path and session observation.",
        });
        return Finish(feasibility, findings);
    }

    private static ResponseIntentFeasibilityResult Finish(
        ResponseAssessmentFeasibility feasibility,
        List<ResponseIntentFeasibilityFinding> findings)
        => new()
        {
            Feasibility = feasibility,
            Findings = findings,
        };
}
