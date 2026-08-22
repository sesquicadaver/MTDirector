using System.Globalization;

namespace Mfc.Domain.Routing;

/// <summary>
/// Evaluates declarative <see cref="RouteExpectation"/> probes against <see cref="RouteResolutionTrace"/> results (M7.1 Spec §11).
/// Zone resolution is routing-only: when <see cref="RouteExpectation.AllowedEgressZones"/> is non-empty,
/// zone names are matched against egress interface names as a proxy (no zone engine).
/// </summary>
public static class RouteExpectationEvaluator
{
    public static IReadOnlyList<RouteFinding> Evaluate(
        IReadOnlyList<RouteExpectation> expectations,
        IReadOnlyList<RouteResolutionTrace> traces,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(traces);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operational);

        List<RouteFinding> findings = [];
        foreach (RouteExpectation expectation in expectations)
        {
            foreach (RouteResolutionTrace trace in traces)
            {
                if (!MatchesExpectation(expectation, trace))
                {
                    continue;
                }

                findings.AddRange(EvaluateMatched(expectation, trace, configuration, operational));
            }
        }

        return findings;
    }

    private static bool MatchesExpectation(RouteExpectation expectation, RouteResolutionTrace trace)
    {
        if (!string.Equals(expectation.Family, trace.Family, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectation.SourceAddress)
            && !string.Equals(
                expectation.SourceAddress.Trim(),
                trace.SourceAddress?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(trace.DestinationAddress))
        {
            return false;
        }

        if (!RouteResolutionPrefixMatcher.TryParseAddress(
                expectation.Family,
                trace.DestinationAddress,
                out UInt128 destination))
        {
            return false;
        }

        return RouteResolutionPrefixMatcher.Contains(expectation.Family, expectation.DestinationPrefix, destination);
    }

    private static IEnumerable<RouteFinding> EvaluateMatched(
        RouteExpectation expectation,
        RouteResolutionTrace trace,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational)
    {
        string subject = trace.DestinationAddress ?? expectation.DestinationPrefix;

        if (!string.IsNullOrWhiteSpace(expectation.ExpectedVrf)
            && !string.Equals(
                expectation.ExpectedVrf.Trim(),
                trace.SelectedVrf?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            yield return Finding(
                expectation,
                RouteExpectationCodes.ExpectedVrfMismatch,
                RouteExpectationCodes.ExpectedVrfMismatchCritical,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected VRF '{expectation.ExpectedVrf}' but trace selected '{trace.SelectedVrf ?? "<none>"}'."),
                subject);
        }

        if (!string.IsNullOrWhiteSpace(expectation.ExpectedTable)
            && !string.Equals(
                expectation.ExpectedTable.Trim(),
                trace.SelectedTable?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            yield return Finding(
                expectation,
                RouteExpectationCodes.ExpectedTableMismatch,
                RouteExpectationCodes.ExpectedTableMismatchCritical,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected table '{expectation.ExpectedTable}' but trace selected '{trace.SelectedTable ?? "<none>"}'."),
                subject);
        }

        if (expectation.AllowedNextHops.Count > 0)
        {
            HashSet<string> allowed = ToNormalizedSet(expectation.AllowedNextHops);
            HashSet<string> observed = CollectNextHops(trace);
            if (observed.Count == 0 || !observed.Any(allowed.Contains))
            {
                yield return Finding(
                    expectation,
                    RouteExpectationCodes.AllowedNextHopViolation,
                    RouteExpectationCodes.AllowedNextHopViolationCritical,
                    "No immediate next hop intersects allowed_next_hops.",
                    subject);
            }
        }

        if (expectation.AllowedEgressInterfaces.Count > 0)
        {
            HashSet<string> allowed = ToNormalizedSet(expectation.AllowedEgressInterfaces);
            HashSet<string> egress = ToNormalizedSet(trace.EgressInterfaces);
            if (egress.Count == 0 || !egress.Overlaps(allowed))
            {
                yield return Finding(
                    expectation,
                    RouteExpectationCodes.AllowedEgressInterfaceViolation,
                    RouteExpectationCodes.AllowedEgressInterfaceViolationCritical,
                    "Egress interfaces do not intersect allowed_egress_interfaces.",
                    subject);
            }
        }

        if (expectation.AllowedEgressZones.Count > 0)
        {
            HashSet<string> allowedZones = ToNormalizedSet(expectation.AllowedEgressZones);
            HashSet<string> egress = ToNormalizedSet(trace.EgressInterfaces);
            if (egress.Count == 0 || !egress.Overlaps(allowedZones))
            {
                yield return Finding(
                    expectation,
                    RouteExpectationCodes.AllowedEgressZoneViolation,
                    RouteExpectationCodes.AllowedEgressZoneViolationCritical,
                    "Egress interfaces do not match allowed_egress_zones (interface-name proxy; zone engine deferred).",
                    subject);
            }
        }

        if (expectation.RequiredRouteTypes.Count > 0)
        {
            HashSet<string> required = ToNormalizedSet(expectation.RequiredRouteTypes);
            HashSet<string> observed = CollectObservedRouteTypes(trace);
            if (!observed.Overlaps(required))
            {
                yield return Finding(
                    expectation,
                    RouteExpectationCodes.RequiredRouteTypeMissing,
                    RouteExpectationCodes.RequiredRouteTypeMissingCritical,
                    "Trace does not satisfy required_route_types.",
                    subject);
            }
        }

        if (expectation.ForbiddenRouteTypes.Count > 0)
        {
            HashSet<string> forbidden = ToNormalizedSet(expectation.ForbiddenRouteTypes);
            HashSet<string> observed = CollectObservedRouteTypes(trace);
            if (observed.Overlaps(forbidden))
            {
                yield return Finding(
                    expectation,
                    RouteExpectationCodes.ForbiddenRouteTypePresent,
                    RouteExpectationCodes.ForbiddenRouteTypePresentCritical,
                    "Trace matches forbidden_route_types.",
                    subject);
            }
        }

        if (expectation.RequireCpuFirewallPath
            && string.Equals(trace.ExecutionPath, RouteResolutionExecutionPaths.Hardware, StringComparison.Ordinal))
        {
            yield return Finding(
                expectation,
                RouteExpectationCodes.CpuFirewallPathRequired,
                RouteExpectationCodes.CpuFirewallPathRequiredCritical,
                "Execution path is HARDWARE-only but CPU firewall path is required.",
                subject);
        }

        if (expectation.RequireReversePath
            && !string.IsNullOrWhiteSpace(trace.SourceAddress)
            && !string.IsNullOrWhiteSpace(trace.DestinationAddress))
        {
            ReversePathSymmetryAnalysis analysis = ReversePathSymmetryAnalyzer.Analyze(
                trace,
                configuration,
                operational,
                new ReversePathSymmetryAnalyzerOptions
                {
                    ExpectAsymmetricReversePath = expectation.ExpectAsymmetricReversePath,
                });

            switch (analysis.Result)
            {
                case ReversePathSymmetryResults.ReversePathMissing:
                    yield return Finding(
                        expectation,
                        RouteExpectationCodes.ReversePathMissing,
                        RouteExpectationCodes.ReversePathMissingCritical,
                        analysis.Detail ?? "Reverse route trace returned NO_ROUTE.",
                        subject);
                    break;
                case ReversePathSymmetryResults.AsymmetricUnexpected:
                    yield return Finding(
                        expectation,
                        RouteExpectationCodes.AsymmetricReversePathUnexpected,
                        RouteExpectationCodes.AsymmetricReversePathUnexpectedCritical,
                        analysis.Detail ?? "Reverse path is asymmetric and not marked as expected.",
                        subject);
                    break;
            }
        }
    }

    private static RouteFinding Finding(
        RouteExpectation expectation,
        string warningCode,
        string criticalCode,
        string message,
        string? subject)
        => new()
        {
            Code = expectation.Critical ? criticalCode : warningCode,
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

    private static HashSet<string> CollectNextHops(RouteResolutionTrace trace)
    {
        HashSet<string> hops = new(StringComparer.OrdinalIgnoreCase);
        foreach (ImmediateNextHop hop in trace.ImmediateNextHops)
        {
            AddGateway(hops, hop.Gateway);
        }

        foreach (SelectedRoute route in trace.SelectedRoutes)
        {
            AddGateway(hops, route.Gateway);
            AddGateway(hops, ParseImmediateGateway(route.ImmediateGateway));
        }

        if (trace.EcmpRouteSet is not null)
        {
            foreach (EcmpNextHop hop in trace.EcmpRouteSet.ActiveNextHops)
            {
                AddGateway(hops, hop.Gateway);
            }
        }

        return hops;
    }

    private static void AddGateway(HashSet<string> hops, string? gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            return;
        }

        hops.Add(NormalizeGateway(gateway));
    }

    private static string NormalizeGateway(string gateway)
    {
        string trimmed = gateway.Trim();
        int percent = trimmed.IndexOf('%', StringComparison.Ordinal);
        return percent >= 0 ? trimmed[..percent] : trimmed;
    }

    private static string? ParseImmediateGateway(string? immediateGateway)
    {
        if (string.IsNullOrWhiteSpace(immediateGateway))
        {
            return null;
        }

        return NormalizeGateway(immediateGateway);
    }

    private static HashSet<string> CollectObservedRouteTypes(RouteResolutionTrace trace)
    {
        HashSet<string> observed = new(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(trace.Decision))
        {
            observed.Add(trace.Decision.Trim());
            if (string.Equals(trace.Decision, RouteResolutionDecisions.Blackhole, StringComparison.Ordinal)
                && string.Equals(trace.RoutingRuleAction, RoutingRuleActions.Drop, StringComparison.OrdinalIgnoreCase))
            {
                observed.Add("DROP");
            }
        }

        foreach (SelectedRoute route in trace.SelectedRoutes)
        {
            if (!string.IsNullOrWhiteSpace(route.Origin))
            {
                observed.Add(route.Origin.Trim());
            }
        }

        return observed;
    }
}
