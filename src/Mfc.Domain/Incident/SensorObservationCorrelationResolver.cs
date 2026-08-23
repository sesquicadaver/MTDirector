using System.Globalization;
using Mfc.Domain.Routing;

namespace Mfc.Domain.Incident;

/// <summary>
/// Correlates a Wazuh/Suricata sensor observation point with a <see cref="RouteResolutionTrace"/>
/// (M7.3-04 / M7.1 §16). Scripted trace input only; no routing writes.
/// </summary>
public static class SensorObservationCorrelationResolver
{
    public const string AnalyzerVersion = "mfc.sensor-observation-correlation.v1";

    /// <summary>Correlates <paramref name="query"/> against the supplied route trace.</summary>
    public static SensorObservationCorrelationResult Correlate(SensorObservationCorrelationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.OriginalFlow);

        if (string.IsNullOrWhiteSpace(query.OriginalFlow.DestinationAddress))
        {
            throw new DomainInvariantException(
                $"{SensorObservationCorrelationCodes.MissingOriginalFlow}: original flow destination is required.");
        }

        List<SensorObservationCorrelationFinding> findings = [];
        bool mismatch = false;
        bool bypass = false;
        bool indeterminate = false;

        RouteResolutionTrace? trace = query.RouteTrace;
        if (trace is null)
        {
            findings.Add(Finding(
                SensorObservationCorrelationCodes.NoRouteTrace,
                "Route resolution trace is required for sensor observation correlation.",
                query.ObservationPoint.ToString()));
            return Result(SensorObservationCorrelationStatus.Indeterminate, findings);
        }

        if (string.Equals(trace.ExecutionPath, RouteResolutionExecutionPaths.Hardware, StringComparison.Ordinal))
        {
            bypass = true;
            findings.Add(Finding(
                SensorObservationCorrelationCodes.SensorBypassHwOffload,
                "Hardware-offloaded forwarding path may bypass CPU-visible sensor observation.",
                trace.ExecutionPath));
        }

        switch (query.ObservationPoint)
        {
            case SensorObservationPoint.Prerouting:
                mismatch |= EvaluatePrerouting(query, trace, findings, ref indeterminate);
                break;
            case SensorObservationPoint.PostDstNat:
                mismatch |= EvaluatePostDstNat(query, trace, findings, ref indeterminate);
                break;
            case SensorObservationPoint.PostRouting:
                mismatch |= EvaluatePostRouting(query, trace, findings, ref indeterminate);
                break;
            case SensorObservationPoint.Egress:
                mismatch |= EvaluateEgress(query, trace, findings, ref indeterminate);
                break;
            default:
                indeterminate = true;
                findings.Add(Finding(
                    SensorObservationCorrelationCodes.InsufficientObservationContext,
                    $"Unsupported observation point '{query.ObservationPoint}'.",
                    query.ObservationPoint.ToString()));
                break;
        }

        mismatch |= EvaluateSharedContext(query, trace, findings);

        if (mismatch)
        {
            return Result(SensorObservationCorrelationStatus.Mismatched, findings);
        }

        if (bypass)
        {
            return Result(SensorObservationCorrelationStatus.SensorBypassed, findings);
        }

        if (indeterminate)
        {
            return Result(SensorObservationCorrelationStatus.Indeterminate, findings);
        }

        findings.Add(Finding(
            SensorObservationCorrelationCodes.CorrelationAligned,
            "Sensor observation aligns with the route resolution trace.",
            FormatSubject(query)));
        return Result(SensorObservationCorrelationStatus.Aligned, findings);
    }

    private static bool EvaluatePrerouting(
        SensorObservationCorrelationQuery query,
        RouteResolutionTrace trace,
        List<SensorObservationCorrelationFinding> findings,
        ref bool indeterminate)
    {
        bool mismatch = false;
        string expectedDestination = query.OriginalFlow.DestinationAddress!.Trim();
        mismatch |= CompareDestination(
            expectedDestination,
            trace.DestinationAddress,
            findings,
            "prerouting original flow destination");

        mismatch |= CompareOptionalString(
            query.IngressInterface,
            trace.IngressInterface,
            SensorObservationCorrelationCodes.IngressInterfaceMismatch,
            "ingress interface",
            findings);

        mismatch |= CompareOptionalString(
            query.RoutingMark,
            trace.RoutingMark,
            SensorObservationCorrelationCodes.RoutingMarkMismatch,
            "routing mark",
            findings);

        if (!string.IsNullOrWhiteSpace(query.Vrf))
        {
            string? traceVrf = trace.InitialVrf ?? trace.SelectedVrf;
            if (traceVrf is null)
            {
                indeterminate = true;
                findings.Add(Finding(
                    SensorObservationCorrelationCodes.InsufficientObservationContext,
                    "Route trace does not report VRF at prerouting observation point.",
                    query.Vrf));
            }
            else
            {
                mismatch |= CompareOptionalString(
                    query.Vrf,
                    traceVrf,
                    SensorObservationCorrelationCodes.VrfMismatch,
                    "VRF",
                    findings);
            }
        }

        return mismatch;
    }

    private static bool EvaluatePostDstNat(
        SensorObservationCorrelationQuery query,
        RouteResolutionTrace trace,
        List<SensorObservationCorrelationFinding> findings,
        ref bool indeterminate)
    {
        if (query.TranslatedFlow is null
            || string.IsNullOrWhiteSpace(query.TranslatedFlow.DestinationAddress))
        {
            indeterminate = true;
            findings.Add(Finding(
                SensorObservationCorrelationCodes.MissingTranslatedFlow,
                "Post-dstnat observation requires translated flow destination.",
                query.OriginalFlow.DestinationAddress));
            return false;
        }

        bool mismatch = CompareDestination(
            query.TranslatedFlow.DestinationAddress.Trim(),
            trace.DestinationAddress,
            findings,
            "post-dstnat translated flow destination");

        string originalDestination = query.OriginalFlow.DestinationAddress!.Trim();
        string translatedDestination = query.TranslatedFlow.DestinationAddress.Trim();
        if (string.Equals(originalDestination, translatedDestination, StringComparison.OrdinalIgnoreCase))
        {
            indeterminate = true;
            findings.Add(Finding(
                SensorObservationCorrelationCodes.InsufficientObservationContext,
                "Post-dstnat observation reports identical original and translated destinations.",
                translatedDestination));
        }

        return mismatch;
    }

    private static bool EvaluatePostRouting(
        SensorObservationCorrelationQuery query,
        RouteResolutionTrace trace,
        List<SensorObservationCorrelationFinding> findings,
        ref bool indeterminate)
    {
        bool mismatch = false;
        string flowDestination = query.TranslatedFlow?.DestinationAddress?.Trim()
            ?? query.OriginalFlow.DestinationAddress!.Trim();
        mismatch |= CompareDestination(flowDestination, trace.DestinationAddress, findings, "post-routing flow destination");

        mismatch |= CompareOptionalString(
            query.SelectedTable,
            trace.SelectedTable,
            SensorObservationCorrelationCodes.RoutingTableMismatch,
            "routing table",
            findings);

        if (!string.IsNullOrWhiteSpace(query.Vrf))
        {
            if (string.IsNullOrWhiteSpace(trace.SelectedVrf))
            {
                indeterminate = true;
                findings.Add(Finding(
                    SensorObservationCorrelationCodes.InsufficientObservationContext,
                    "Route trace does not report selected VRF at post-routing observation point.",
                    query.Vrf));
            }
            else
            {
                mismatch |= CompareOptionalString(
                    query.Vrf,
                    trace.SelectedVrf,
                    SensorObservationCorrelationCodes.VrfMismatch,
                    "VRF",
                    findings);
            }
        }

        return mismatch;
    }

    private static bool EvaluateEgress(
        SensorObservationCorrelationQuery query,
        RouteResolutionTrace trace,
        List<SensorObservationCorrelationFinding> findings,
        ref bool indeterminate)
    {
        if (string.IsNullOrWhiteSpace(query.EgressInterface))
        {
            indeterminate = true;
            findings.Add(Finding(
                SensorObservationCorrelationCodes.InsufficientObservationContext,
                "Egress observation point requires sensor-reported egress interface.",
                query.ObservationPoint.ToString()));
            return false;
        }

        if (trace.EgressInterfaces.Count == 0)
        {
            indeterminate = true;
            findings.Add(Finding(
                SensorObservationCorrelationCodes.InsufficientObservationContext,
                "Route trace does not report egress interfaces.",
                query.EgressInterface));
            return false;
        }

        bool matches = trace.EgressInterfaces.Any(egress =>
            string.Equals(egress.Trim(), query.EgressInterface.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!matches)
        {
            findings.Add(Finding(
                SensorObservationCorrelationCodes.EgressInterfaceMismatch,
                $"Sensor egress '{query.EgressInterface}' is not in trace egress set [{string.Join(", ", trace.EgressInterfaces)}].",
                query.EgressInterface));
            return true;
        }

        return false;
    }

    private static bool EvaluateSharedContext(
        SensorObservationCorrelationQuery query,
        RouteResolutionTrace trace,
        List<SensorObservationCorrelationFinding> findings)
    {
        if (query.ObservationPoint == SensorObservationPoint.Egress)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.EgressInterface) || trace.EgressInterfaces.Count == 0)
        {
            return false;
        }

        bool matches = trace.EgressInterfaces.Any(egress =>
            string.Equals(egress.Trim(), query.EgressInterface.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!matches)
        {
            findings.Add(Finding(
                SensorObservationCorrelationCodes.EgressInterfaceMismatch,
                $"Sensor-reported egress '{query.EgressInterface}' differs from trace egress path.",
                query.EgressInterface));
            return true;
        }

        return false;
    }

    private static bool CompareDestination(
        string expected,
        string? actual,
        List<SensorObservationCorrelationFinding> findings,
        string subject)
    {
        if (string.IsNullOrWhiteSpace(actual))
        {
            findings.Add(Finding(
                SensorObservationCorrelationCodes.InsufficientObservationContext,
                $"Route trace destination is missing for {subject}.",
                expected));
            return false;
        }

        if (!string.Equals(expected, actual.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Finding(
                SensorObservationCorrelationCodes.FlowDestinationMismatch,
                $"{subject} '{expected}' does not match trace destination '{actual}'.",
                expected));
            return true;
        }

        return false;
    }

    private static bool CompareOptionalString(
        string? expected,
        string? actual,
        string mismatchCode,
        string label,
        List<SensorObservationCorrelationFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        if (!string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Finding(
                mismatchCode,
                $"Sensor {label} '{expected}' does not match trace {label} '{actual}'.",
                expected));
            return true;
        }

        return false;
    }

    private static string FormatSubject(SensorObservationCorrelationQuery query)
    {
        string destination = query.TranslatedFlow?.DestinationAddress
            ?? query.OriginalFlow.DestinationAddress
            ?? "*";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{query.ObservationPoint} -> {destination}");
    }

    private static SensorObservationCorrelationFinding Finding(string code, string message, string? subject)
        => new()
        {
            Code = code,
            Message = message,
            Subject = subject,
        };

    private static SensorObservationCorrelationResult Result(
        SensorObservationCorrelationStatus status,
        List<SensorObservationCorrelationFinding> findings)
        => new()
        {
            Status = status,
            Findings = findings,
        };
}
