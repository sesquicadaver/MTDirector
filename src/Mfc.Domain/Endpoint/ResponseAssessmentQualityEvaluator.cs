using Mfc.Domain.Incident;
using Mfc.Domain.Routing;

namespace Mfc.Domain.Endpoint;

/// <summary>
/// Derives visibility_status and confidence for response assessments (M7.3-05 / next-2).
/// Fails closed when observation inputs are incomplete.
/// </summary>
public static class ResponseAssessmentQualityEvaluator
{
    public const string AnalyzerVersion = "mfc.response-assessment-quality.v1";

    /// <summary>Evaluates quality signals for one assessment context.</summary>
    public static ResponseAssessmentQualityResult Evaluate(ResponseAssessmentQualityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        List<ResponseAssessmentQualityFinding> findings = [];
        AssessmentVisibilityStatus visibility = AssessmentVisibilityStatus.Full;
        int confidence = BaseConfidence(input.Feasibility);

        if (input.Feasibility == ResponseAssessmentFeasibility.Indeterminate)
        {
            visibility = MaxVisibility(visibility, AssessmentVisibilityStatus.Partial);
            confidence -= 25;
            findings.Add(Finding(
                ResponseAssessmentQualityCodes.IndeterminateFeasibility,
                "Feasibility is indeterminate; confidence is reduced.",
                input.Feasibility.ToString()));
        }

        switch (input.SessionVisibility)
        {
            case SessionVisibilityStatus.NotObserved:
                visibility = AssessmentVisibilityStatus.NotObserved;
                confidence -= 30;
                findings.Add(Finding(
                    ResponseAssessmentQualityCodes.SessionNotObserved,
                    "Connection-tracking session was not observed for the incident flow.",
                    nameof(SessionVisibilityStatus.NotObserved)));
                break;
            case SessionVisibilityStatus.Partial:
                visibility = MaxVisibility(visibility, AssessmentVisibilityStatus.Partial);
                confidence -= 15;
                findings.Add(Finding(
                    ResponseAssessmentQualityCodes.LimitedSessionVisibility,
                    "Connection-tracking session visibility is partial.",
                    nameof(SessionVisibilityStatus.Partial)));
                break;
            case SessionVisibilityStatus.Full:
                break;
            case null:
                visibility = MaxVisibility(visibility, AssessmentVisibilityStatus.Partial);
                confidence -= 10;
                break;
        }

        if (input.RouteTrace is not null)
        {
            if (string.Equals(
                    input.RouteTrace.ExecutionPath,
                    RouteResolutionExecutionPaths.Hardware,
                    StringComparison.Ordinal))
            {
                visibility = MaxVisibility(visibility, AssessmentVisibilityStatus.Partial);
                confidence -= 20;
                findings.Add(Finding(
                    ResponseAssessmentQualityCodes.HardwareOffloadLimitedVisibility,
                    "Hardware-offloaded route execution path limits CPU-visible observation.",
                    input.RouteTrace.ExecutionPath));
            }

            if (string.Equals(
                    input.RouteTrace.Certainty,
                    RouteResolutionCertainties.Indeterminate,
                    StringComparison.Ordinal))
            {
                visibility = MaxVisibility(visibility, AssessmentVisibilityStatus.Partial);
                confidence -= 10;
                findings.Add(Finding(
                    ResponseAssessmentQualityCodes.IndeterminateRouteCertainty,
                    "Route resolution certainty is indeterminate.",
                    input.RouteTrace.Certainty));
            }
        }
        else
        {
            visibility = MaxVisibility(visibility, AssessmentVisibilityStatus.Partial);
            confidence -= 10;
        }

        switch (input.PacketPathClass)
        {
            case ObservedPacketPathClass.HardwareOffloaded:
                visibility = MaxVisibility(visibility, AssessmentVisibilityStatus.Partial);
                confidence -= 20;
                findings.Add(Finding(
                    ResponseAssessmentQualityCodes.HardwareOffloadedPacketPath,
                    "Packet path is hardware-offloaded.",
                    input.PacketPathClass.ToString()));
                break;
            case ObservedPacketPathClass.Mixed:
                visibility = MaxVisibility(visibility, AssessmentVisibilityStatus.Partial);
                confidence -= 15;
                findings.Add(Finding(
                    ResponseAssessmentQualityCodes.MixedPacketPath,
                    "Packet path classification is mixed.",
                    input.PacketPathClass.ToString()));
                break;
            case ObservedPacketPathClass.Indeterminate:
                visibility = MaxVisibility(visibility, AssessmentVisibilityStatus.Partial);
                confidence -= 15;
                findings.Add(Finding(
                    ResponseAssessmentQualityCodes.IndeterminatePacketPath,
                    "Packet path classification is indeterminate.",
                    input.PacketPathClass.ToString()));
                break;
            case ObservedPacketPathClass.CpuFirewall:
            case ObservedPacketPathClass.Unknown:
                break;
        }

        if (visibility == AssessmentVisibilityStatus.Full)
        {
            confidence += 10;
        }

        findings.Add(Finding(
            ResponseAssessmentQualityCodes.QualityEvaluated,
            "Response assessment visibility and confidence evaluated.",
            visibility.ToString()));

        return new ResponseAssessmentQualityResult
        {
            VisibilityStatus = visibility,
            Confidence = ClampConfidence(confidence),
            Findings = findings,
        };
    }

    private static int BaseConfidence(ResponseAssessmentFeasibility feasibility)
        => feasibility switch
        {
            ResponseAssessmentFeasibility.FullyEnforceable => 85,
            ResponseAssessmentFeasibility.NewConnectionsOnly => 60,
            ResponseAssessmentFeasibility.NotEnforceableByIpFilter => 80,
            ResponseAssessmentFeasibility.Indeterminate => 30,
            _ => 30,
        };

    private static AssessmentVisibilityStatus MaxVisibility(
        AssessmentVisibilityStatus current,
        AssessmentVisibilityStatus candidate)
        => (AssessmentVisibilityStatus)Math.Max((int)current, (int)candidate);

    private static int ClampConfidence(int value) => Math.Clamp(value, 0, 100);

    private static ResponseAssessmentQualityFinding Finding(string code, string message, string? subject)
        => new()
        {
            Code = code,
            Message = message,
            Subject = subject,
        };
}
