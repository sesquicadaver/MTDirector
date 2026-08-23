using Mfc.Domain.Endpoint;
using Mfc.Domain.Routing;

namespace Mfc.Domain.Incident;

/// <summary>
/// Maps packet-path and session observation to response feasibility (next-2 §ResponseAssessment table / M7.3-06).
/// </summary>
public static class IncidentResponseFeasibilityClassifier
{
    /// <summary>Classifies enforceability from scripted observation inputs.</summary>
    public static ResponseAssessmentFeasibility Classify(
        ObservedPacketPathClass packetPathClass,
        SessionVisibilityStatus? sessionVisibility,
        RouteResolutionTrace? routeTrace = null)
    {
        if (packetPathClass == ObservedPacketPathClass.HardwareOffloaded
            || string.Equals(
                routeTrace?.ExecutionPath,
                RouteResolutionExecutionPaths.Hardware,
                StringComparison.Ordinal))
        {
            return ResponseAssessmentFeasibility.NotEnforceableByIpFilter;
        }

        if (sessionVisibility == SessionVisibilityStatus.Partial)
        {
            return ResponseAssessmentFeasibility.NewConnectionsOnly;
        }

        if (packetPathClass is ObservedPacketPathClass.Mixed or ObservedPacketPathClass.Indeterminate
            || packetPathClass == ObservedPacketPathClass.Unknown
            || sessionVisibility == SessionVisibilityStatus.NotObserved)
        {
            return ResponseAssessmentFeasibility.Indeterminate;
        }

        return ResponseAssessmentFeasibility.FullyEnforceable;
    }
}
