using System.Globalization;

namespace Mfc.Domain.Routing;

/// <summary>
/// Evaluates route expectations and latency thresholds for a bound network path (M7.1 Spec §13).
/// When the path fingerprint changed from baseline and latency regressed, emits
/// <see cref="NetworkPathProfileCodes.RoutePathChangedWithLatencyRegression"/> instead of isolated RTT findings.
/// </summary>
public static class NetworkPathLatencyEvaluator
{
    public static IReadOnlyList<RouteFinding> Evaluate(NetworkPathLatencyEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        List<RouteFinding> findings = [];
        findings.AddRange(EvaluateRouteExpectations(input.Profile, input.Trace));
        findings.AddRange(EvaluateLatency(input));
        return findings;
    }

    public static IReadOnlyList<RouteFinding> EvaluateMany(IReadOnlyList<NetworkPathLatencyEvaluationInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        List<RouteFinding> findings = [];
        foreach (NetworkPathLatencyEvaluationInput input in inputs)
        {
            findings.AddRange(Evaluate(input));
        }

        return findings;
    }

    private static IEnumerable<RouteFinding> EvaluateRouteExpectations(
        NetworkPathProfile profile,
        RouteResolutionTrace trace)
    {
        string subject = trace.DestinationAddress ?? profile.Destination;

        if (!string.IsNullOrWhiteSpace(profile.ExpectedRoutePrefix)
            && !string.Equals(
                Normalize(profile.ExpectedRoutePrefix),
                Normalize(trace.MatchedPrefix),
                StringComparison.OrdinalIgnoreCase))
        {
            yield return Finding(
                profile,
                NetworkPathProfileCodes.ExpectedRoutePrefixMismatch,
                NetworkPathProfileCodes.ExpectedRoutePrefixMismatchCritical,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected route prefix '{profile.ExpectedRoutePrefix}' but trace matched '{trace.MatchedPrefix ?? "<none>"}'."),
                subject);
        }

        if (profile.ExpectedNextHops.Count > 0)
        {
            HashSet<string> expected = ToNormalizedSet(profile.ExpectedNextHops);
            HashSet<string> observed = ToNormalizedSet(RoutePathFingerprint.FromTrace(trace).NextHops);
            if (observed.Count == 0 || !observed.Overlaps(expected))
            {
                yield return Finding(
                    profile,
                    NetworkPathProfileCodes.ExpectedNextHopMismatch,
                    NetworkPathProfileCodes.ExpectedNextHopMismatchCritical,
                    "Immediate next hops do not intersect expected_next_hops.",
                    subject);
            }
        }

        if (profile.ExpectedEgressInterfaces.Count > 0)
        {
            HashSet<string> expected = ToNormalizedSet(profile.ExpectedEgressInterfaces);
            HashSet<string> observed = ToNormalizedSet(trace.EgressInterfaces);
            if (observed.Count == 0 || !observed.Overlaps(expected))
            {
                yield return Finding(
                    profile,
                    NetworkPathProfileCodes.ExpectedEgressInterfaceMismatch,
                    NetworkPathProfileCodes.ExpectedEgressInterfaceMismatchCritical,
                    "Egress interfaces do not intersect expected_egress_interfaces.",
                    subject);
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.ExpectedExecutionPath)
            && !string.Equals(
                Normalize(profile.ExpectedExecutionPath),
                Normalize(trace.ExecutionPath),
                StringComparison.OrdinalIgnoreCase))
        {
            yield return Finding(
                profile,
                NetworkPathProfileCodes.ExpectedExecutionPathMismatch,
                NetworkPathProfileCodes.ExpectedExecutionPathMismatchCritical,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected execution path '{profile.ExpectedExecutionPath}' but trace reported '{trace.ExecutionPath ?? "<none>"}'."),
                subject);
        }
    }

    private static IEnumerable<RouteFinding> EvaluateLatency(NetworkPathLatencyEvaluationInput input)
    {
        NetworkPathProfile profile = input.Profile;
        LatencyMeasurement measurement = input.Measurement;
        RoutePathFingerprint currentFingerprint = RoutePathFingerprint.FromTrace(input.Trace);
        bool pathChanged = RoutePathFingerprint.PathChanged(input.BaselinePathFingerprint, currentFingerprint);
        bool latencyRegressed = IsLatencyRegression(profile, measurement, input.BaselineMeasurement);

        if (pathChanged && latencyRegressed)
        {
            yield return Finding(
                profile,
                NetworkPathProfileCodes.RoutePathChangedWithLatencyRegression,
                NetworkPathProfileCodes.RoutePathChangedWithLatencyRegressionCritical,
                "Route path fingerprint changed together with latency regression.",
                input.Trace.DestinationAddress ?? profile.Destination);
            yield break;
        }

        if (profile.MaxLoss is not null && measurement.PacketLossPercent > profile.MaxLoss.Value)
        {
            yield return Finding(
                profile,
                NetworkPathProfileCodes.LatencyLossHigh,
                NetworkPathProfileCodes.LatencyLossHighCritical,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Packet loss {measurement.PacketLossPercent:F2}% exceeds max_loss {profile.MaxLoss.Value:F2}%."),
                input.Trace.DestinationAddress ?? profile.Destination);
        }

        if (profile.MaxRtt is not null && measurement.RoundTripTimeMs > profile.MaxRtt.Value)
        {
            yield return Finding(
                profile,
                NetworkPathProfileCodes.LatencyRttHigh,
                NetworkPathProfileCodes.LatencyRttHighCritical,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"RTT {measurement.RoundTripTimeMs:F2} ms exceeds max_rtt {profile.MaxRtt.Value:F2} ms."),
                input.Trace.DestinationAddress ?? profile.Destination);
        }

        if (profile.MaxJitter is not null && measurement.JitterMs > profile.MaxJitter.Value)
        {
            yield return Finding(
                profile,
                NetworkPathProfileCodes.LatencyJitterHigh,
                NetworkPathProfileCodes.LatencyJitterHighCritical,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Jitter {measurement.JitterMs:F2} ms exceeds max_jitter {profile.MaxJitter.Value:F2} ms."),
                input.Trace.DestinationAddress ?? profile.Destination);
        }
    }

    private static bool IsLatencyRegression(
        NetworkPathProfile profile,
        LatencyMeasurement measurement,
        LatencyMeasurement? baseline)
    {
        if (profile.MaxLoss is not null && measurement.PacketLossPercent > profile.MaxLoss.Value)
        {
            return true;
        }

        if (profile.MaxRtt is not null && measurement.RoundTripTimeMs > profile.MaxRtt.Value)
        {
            return true;
        }

        if (profile.MaxJitter is not null && measurement.JitterMs > profile.MaxJitter.Value)
        {
            return true;
        }

        if (profile.MaxRegression is not null
            && baseline is not null
            && baseline.RoundTripTimeMs > 0)
        {
            double increase = (measurement.RoundTripTimeMs - baseline.RoundTripTimeMs) / baseline.RoundTripTimeMs;
            if (increase > profile.MaxRegression.Value)
            {
                return true;
            }
        }

        return false;
    }

    private static RouteFinding Finding(
        NetworkPathProfile profile,
        string warningCode,
        string criticalCode,
        string message,
        string? subject)
        => new()
        {
            Code = profile.Critical ? criticalCode : warningCode,
            Message = message,
            Subject = subject,
        };

    private static HashSet<string> ToNormalizedSet(IEnumerable<string> values)
    {
        HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            set.Add(value.Trim());
        }

        return set;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
