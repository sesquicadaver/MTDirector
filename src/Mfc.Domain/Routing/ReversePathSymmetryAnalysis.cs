using System.Globalization;

namespace Mfc.Domain.Routing;

/// <summary>
/// Forward/reverse route trace comparison for stateful and multi-WAN assurance (M7.1 Spec §12).
/// Produced by <see cref="ReversePathSymmetryAnalyzer"/>.
/// </summary>
public sealed class ReversePathSymmetryAnalysis
{
    public required string Result { get; init; }

    public RouteResolutionTrace? ReverseTrace { get; init; }

    public string? Detail { get; init; }

    /// <summary>Dimensions that differ between forward and reverse traces (table, vrf, egress, decision).</summary>
    public IReadOnlyList<string> MismatchedDimensions { get; init; } = [];
}

/// <summary>Optional flags for <see cref="ReversePathSymmetryAnalyzer"/>.</summary>
public sealed class ReversePathSymmetryAnalyzerOptions
{
    /// <summary>When true, table/VRF/egress/decision mismatches classify as expected asymmetry.</summary>
    public bool ExpectAsymmetricReversePath { get; init; }
}

/// <summary>
/// Compares forward A→B and reverse B→A route resolution traces (M7.1-07).
/// Operates on Domain snapshots only; never writes routing configuration.
/// </summary>
public static class ReversePathSymmetryAnalyzer
{
    /// <summary>
    /// Analyzes reverse-path symmetry for one forward trace.
    /// Returns <see cref="ReversePathSymmetryResults.Indeterminate"/> when probe endpoints are incomplete
    /// or either trace outcome is indeterminate.
    /// </summary>
    public static ReversePathSymmetryAnalysis Analyze(
        RouteResolutionTrace forwardTrace,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational,
        ReversePathSymmetryAnalyzerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(forwardTrace);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operational);

        if (string.IsNullOrWhiteSpace(forwardTrace.SourceAddress)
            || string.IsNullOrWhiteSpace(forwardTrace.DestinationAddress))
        {
            return new ReversePathSymmetryAnalysis
            {
                Result = ReversePathSymmetryResults.Indeterminate,
                Detail = "Forward trace requires source and destination addresses for reverse-path analysis.",
            };
        }

        RouteResolutionTrace reverseTrace = RouteResolutionTraceEngine.Analyze(
            BuildReverseQuery(forwardTrace),
            configuration,
            operational);

        if (string.Equals(reverseTrace.Decision, RouteResolutionDecisions.NoRoute, StringComparison.Ordinal))
        {
            return new ReversePathSymmetryAnalysis
            {
                Result = ReversePathSymmetryResults.ReversePathMissing,
                ReverseTrace = reverseTrace,
                Detail = "Reverse route trace returned NO_ROUTE.",
            };
        }

        if (IsIndeterminateOutcome(forwardTrace) || IsIndeterminateOutcome(reverseTrace))
        {
            return new ReversePathSymmetryAnalysis
            {
                Result = ReversePathSymmetryResults.Indeterminate,
                ReverseTrace = reverseTrace,
                Detail = "Forward or reverse trace decision/certainty is indeterminate.",
            };
        }

        List<string> mismatches = CollectMismatches(forwardTrace, reverseTrace);
        if (mismatches.Count == 0)
        {
            return new ReversePathSymmetryAnalysis
            {
                Result = ReversePathSymmetryResults.Symmetric,
                ReverseTrace = reverseTrace,
                Detail = "Selected table, VRF, egress interfaces, and decisions match.",
            };
        }

        bool expectAsymmetric = options?.ExpectAsymmetricReversePath ?? false;
        return new ReversePathSymmetryAnalysis
        {
            Result = expectAsymmetric
                ? ReversePathSymmetryResults.AsymmetricExpected
                : ReversePathSymmetryResults.AsymmetricUnexpected,
            ReverseTrace = reverseTrace,
            Detail = string.Create(
                CultureInfo.InvariantCulture,
                $"Reverse path differs on: {string.Join(", ", mismatches)}."),
            MismatchedDimensions = mismatches,
        };
    }

    /// <summary>Attaches symmetry analysis to a forward trace copy for persistence on <see cref="RouteResolutionTrace"/>.</summary>
    public static RouteResolutionTrace AttachAnalysis(
        RouteResolutionTrace forwardTrace,
        ReversePathSymmetryAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(forwardTrace);
        ArgumentNullException.ThrowIfNull(analysis);
        return forwardTrace.WithReversePathSymmetry(analysis);
    }

    private static RouteResolutionQuery BuildReverseQuery(RouteResolutionTrace forward)
    {
        string? reverseIngress = forward.EgressInterfaces.Count > 0
            ? forward.EgressInterfaces[0]
            : forward.IngressInterface;
        return new RouteResolutionQuery
        {
            Family = forward.Family,
            SourceAddress = forward.DestinationAddress,
            DestinationAddress = forward.SourceAddress!,
            IngressInterface = reverseIngress,
            InitialVrf = forward.InitialVrf,
        };
    }

    private static bool IsIndeterminateOutcome(RouteResolutionTrace trace)
        => string.Equals(trace.Decision, RouteResolutionDecisions.Indeterminate, StringComparison.Ordinal)
           || string.Equals(trace.Certainty, RouteResolutionCertainties.Indeterminate, StringComparison.Ordinal);

    private static List<string> CollectMismatches(RouteResolutionTrace forward, RouteResolutionTrace reverse)
    {
        List<string> mismatches = [];
        if (!EqualsNormalized(forward.SelectedTable, reverse.SelectedTable))
        {
            mismatches.Add("table");
        }

        if (!EqualsNormalized(forward.SelectedVrf, reverse.SelectedVrf))
        {
            mismatches.Add("vrf");
        }

        if (!EgressSetsEqual(forward.EgressInterfaces, reverse.EgressInterfaces))
        {
            mismatches.Add("egress");
        }

        if (!EqualsNormalized(forward.Decision, reverse.Decision))
        {
            mismatches.Add("decision");
        }

        return mismatches;
    }

    private static bool EqualsNormalized(string? left, string? right)
        => string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static bool EgressSetsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        HashSet<string> leftSet = ToNormalizedSet(left);
        HashSet<string> rightSet = ToNormalizedSet(right);
        return leftSet.SetEquals(rightSet);
    }

    private static HashSet<string> ToNormalizedSet(IReadOnlyList<string> values)
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
}
