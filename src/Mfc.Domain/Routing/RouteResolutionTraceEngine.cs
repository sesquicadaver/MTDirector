using System.Globalization;
using Mfc.Domain.Inventory;

namespace Mfc.Domain.Routing;

/// <summary>
/// Policy-routing → table lookup → recursive next-hop resolution (M7.1 Spec §4–§9).
/// Operates on Domain configuration/operational snapshots and explicit probe inputs only.
/// </summary>
public static class RouteResolutionTraceEngine
{
    public const string AnalyzerVersion = "mfc.route-resolution.v1";

    private static readonly string[] DefaultDecisionOrder =
    [
        "routing-mark",
        "routing-rule",
        "vrf",
        "main",
    ];

    /// <summary>Resolves one probe against config + operational snapshots.</summary>
    public static RouteResolutionTrace Analyze(
        RouteResolutionQuery query,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operational);
        if (!RouteResolutionPrefixMatcher.TryParseFamily(query.Family, out _))
        {
            throw new DomainInvariantException($"Unsupported route resolution family '{query.Family}'.");
        }

        if (!RouteResolutionPrefixMatcher.TryParseAddress(query.Family, query.DestinationAddress, out UInt128 destination))
        {
            throw new DomainInvariantException("Destination address is required and must be valid.");
        }

        RouteResolutionPrefixMatcher.TryParseAddress(query.Family, query.SourceAddress, out UInt128 source);
        IReadOnlyList<string> decisionOrder = ParseDecisionOrder(configuration.Settings.PolicyRules);
        IReadOnlyList<ResolvedRoute> routes = BuildRouteIndex(configuration, operational, query.Family);
        string? vrfFromIngress = ResolveVrfForIngress(configuration, query.IngressInterface);
        string effectiveVrf = query.InitialVrf ?? vrfFromIngress ?? "main";

        PolicyOutcome policy = ApplyPolicyRouting(
            query,
            configuration,
            routes,
            destination,
            source,
            decisionOrder,
            effectiveVrf);

        return BuildTrace(query, decisionOrder, effectiveVrf, policy, routes, destination);
    }

    /// <summary>Resolves multiple probes in stable query order.</summary>
    public static IReadOnlyList<RouteResolutionTrace> AnalyzeMany(
        IReadOnlyList<RouteResolutionQuery> queries,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational)
    {
        ArgumentNullException.ThrowIfNull(queries);
        List<RouteResolutionTrace> traces = new(queries.Count);
        foreach (RouteResolutionQuery query in queries)
        {
            traces.Add(Analyze(query, configuration, operational));
        }

        return traces;
    }

    private static string[] ParseDecisionOrder(string? policyRules)
    {
        if (string.IsNullOrWhiteSpace(policyRules))
        {
            return DefaultDecisionOrder;
        }

        string trimmed = policyRules.Trim();
        if (trimmed.Equals("lookup", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultDecisionOrder;
        }

        string[] parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? DefaultDecisionOrder : parts;
    }

    private static string? ResolveVrfForIngress(RoutingConfigurationSnapshot configuration, string? ingressInterface)
    {
        if (string.IsNullOrWhiteSpace(ingressInterface))
        {
            return null;
        }

        string ingress = ingressInterface.Trim();
        foreach (VrfDefinitionFact vrf in configuration.Vrfs)
        {
            if (IsDisabled(vrf.Disabled) || string.IsNullOrWhiteSpace(vrf.Interfaces))
            {
                continue;
            }

            foreach (string bound in vrf.Interfaces.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(bound, ingress, StringComparison.Ordinal))
                {
                    return vrf.Name;
                }
            }
        }

        return null;
    }

    private static PolicyOutcome ApplyPolicyRouting(
        RouteResolutionQuery query,
        RoutingConfigurationSnapshot configuration,
        IReadOnlyList<ResolvedRoute> routes,
        UInt128 destination,
        UInt128 source,
        IReadOnlyList<string> decisionOrder,
        string effectiveVrf)
    {
        PolicyOutcome outcome = new()
        {
            SelectedVrf = effectiveVrf,
            RoutingMark = query.RoutingMark,
            MatchedMangleRule = query.MatchedMangleRule,
        };

        foreach (string stage in decisionOrder)
        {
            switch (stage.Trim().ToLowerInvariant())
            {
                case "routing-mark":
                case "mangle":
                    if (!string.IsNullOrWhiteSpace(query.RoutingMark)
                        && TryPolicyFromRoutingMark(query, configuration, routes, destination, source, outcome))
                    {
                        return outcome;
                    }

                    break;
                case "routing-rule":
                case "rule":
                    if (TryPolicyFromRoutingRules(query, configuration, routes, destination, source, outcome))
                    {
                        return outcome;
                    }

                    break;
                case "vrf":
                    if (TryPolicyFromVrf(query, configuration, routes, destination, outcome))
                    {
                        return outcome;
                    }

                    break;
                case "main":
                case "local":
                    if (TryLookup(query.Family, "main", routes, destination, outcome))
                    {
                        return outcome;
                    }

                    break;
            }
        }

        outcome.Decision = RouteResolutionDecisions.NoRoute;
        outcome.Certainty = RouteResolutionCertainties.Definite;
        return outcome;
    }

    private static bool TryPolicyFromRoutingMark(
        RouteResolutionQuery query,
        RoutingConfigurationSnapshot configuration,
        IReadOnlyList<ResolvedRoute> routes,
        UInt128 destination,
        UInt128 source,
        PolicyOutcome outcome)
    {
        string mark = query.RoutingMark!.Trim();
        RoutingRuleFact? matchedRule = configuration.Rules
            .Where(static r => !IsDisabled(r.Disabled))
            .OrderBy(static r => r.EffectiveOrdinal)
            .FirstOrDefault(r => MatchesRoutingRule(r, query, mark, destination, source));
        if (matchedRule is not null)
        {
            return ApplyRoutingRuleAction(matchedRule, query.Family, routes, destination, outcome);
        }

        if (TableExists(configuration, mark) && TryLookup(query.Family, mark, routes, destination, outcome))
        {
            outcome.SelectedTable = mark;
            return true;
        }

        return false;
    }

    private static bool TryPolicyFromRoutingRules(
        RouteResolutionQuery query,
        RoutingConfigurationSnapshot configuration,
        IReadOnlyList<ResolvedRoute> routes,
        UInt128 destination,
        UInt128 source,
        PolicyOutcome outcome)
    {
        foreach (RoutingRuleFact rule in configuration.Rules.Where(static r => !IsDisabled(r.Disabled)).OrderBy(static r => r.EffectiveOrdinal))
        {
            if (!MatchesRoutingRule(rule, query, query.RoutingMark, destination, source))
            {
                continue;
            }

            if (ApplyRoutingRuleAction(rule, query.Family, routes, destination, outcome))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryPolicyFromVrf(
        RouteResolutionQuery query,
        RoutingConfigurationSnapshot configuration,
        IReadOnlyList<ResolvedRoute> routes,
        UInt128 destination,
        PolicyOutcome outcome)
    {
        string vrf = outcome.SelectedVrf ?? query.InitialVrf ?? "main";
        if (string.IsNullOrWhiteSpace(vrf))
        {
            return false;
        }

        outcome.SelectedVrf = vrf;
        if (TryLookup(query.Family, vrf, routes, destination, outcome))
        {
            return true;
        }

        return TableExists(configuration, vrf) && TryLookup(query.Family, vrf, routes, destination, outcome);
    }

    private static bool ApplyRoutingRuleAction(
        RoutingRuleFact rule,
        string family,
        IReadOnlyList<ResolvedRoute> routes,
        UInt128 destination,
        PolicyOutcome outcome)
    {
        string action = NormalizeAction(rule.Action);
        outcome.MatchedRoutingRule = new MatchedRoutingRule
        {
            Ordinal = rule.EffectiveOrdinal,
            Action = action,
            Table = rule.Table,
            RoutingMark = rule.RoutingMark,
        };
        outcome.RoutingRuleAction = action;

        switch (action)
        {
            case RoutingRuleActions.Drop:
                outcome.Decision = RouteResolutionDecisions.Blackhole;
                outcome.Certainty = RouteResolutionCertainties.Definite;
                return true;
            case RoutingRuleActions.Unreachable:
                outcome.Decision = RouteResolutionDecisions.Unreachable;
                outcome.Certainty = RouteResolutionCertainties.Definite;
                return true;
            case RoutingRuleActions.LookupOnly:
                if (!string.IsNullOrWhiteSpace(rule.Table) && TryLookup(family, rule.Table!, routes, destination, outcome))
                {
                    return true;
                }

                outcome.Decision = RouteResolutionDecisions.NoRoute;
                outcome.Certainty = RouteResolutionCertainties.Definite;
                return true;
            case RoutingRuleActions.Lookup:
            default:
                if (!string.IsNullOrWhiteSpace(rule.Table) && TryLookup(family, rule.Table!, routes, destination, outcome))
                {
                    return true;
                }

                return false;
        }
    }

    private static bool TryLookup(
        string family,
        string table,
        IReadOnlyList<ResolvedRoute> routes,
        UInt128 destination,
        PolicyOutcome outcome)
    {
        outcome.SelectedTable = table;
        LookupResult lookup = SelectRoutes(family, table, routes, destination);
        outcome.Candidates = lookup.Candidates;
        outcome.Selected = lookup.Selected;
        outcome.MatchedPrefix = lookup.MatchedPrefix;
        if (lookup.Selected.Count == 0)
        {
            return false;
        }

        outcome.Decision = ClassifyRouteDecision(lookup.Selected[0]);
        if (outcome.Decision is RouteResolutionDecisions.Blackhole
            or RouteResolutionDecisions.Prohibit
            or RouteResolutionDecisions.Unreachable
            or RouteResolutionDecisions.LocalDelivery)
        {
            outcome.Certainty = RouteResolutionCertainties.Definite;
            outcome.RecursiveSteps = [];
            outcome.ImmediateNextHops = BuildImmediateNextHops(lookup.Selected, outcome.Decision);
            outcome.EgressInterfaces = ExtractInterfaces(outcome.ImmediateNextHops);
            outcome.ExecutionPath = ClassifyExecutionPath(lookup.Selected);
            return true;
        }

        RecursiveOutcome recursive = ResolveRecursive(family, table, routes, lookup.Selected);
        outcome.RecursiveSteps = recursive.Steps;
        outcome.ImmediateNextHops = recursive.ImmediateNextHops;
        outcome.EgressInterfaces = recursive.EgressInterfaces;
        outcome.ExecutionPath = recursive.ExecutionPath;
        outcome.Certainty = recursive.Certainty;
        outcome.Decision = recursive.Decision ?? RouteResolutionDecisions.Forward;
        outcome.EcmpMembers = recursive.EcmpMembers;
        outcome.PreferredSource = lookup.Selected[0].PreferredSource;
        return true;
    }

    private static LookupResult SelectRoutes(
        string family,
        string table,
        IReadOnlyList<ResolvedRoute> routes,
        UInt128 destination)
    {
        List<ResolvedRoute> tableRoutes = routes
            .Where(r => string.Equals(r.Table, table, StringComparison.Ordinal)
                        && string.Equals(r.Family, family, StringComparison.OrdinalIgnoreCase)
                        && r.Active
                        && !IsDisabled(r.Disabled))
            .ToList();
        if (tableRoutes.Count == 0)
        {
            return LookupResult.Empty;
        }

        int bestPrefix = -1;
        int? bestDistance = null;
        foreach (ResolvedRoute route in tableRoutes)
        {
            int prefixLength = RouteResolutionPrefixMatcher.PrefixLength(family, route.DstPrefix);
            if (prefixLength < 0 || !RouteResolutionPrefixMatcher.Contains(family, route.DstPrefix, destination))
            {
                continue;
            }

            int distance = route.Distance ?? 1;
            if (prefixLength > bestPrefix)
            {
                bestPrefix = prefixLength;
                bestDistance = distance;
            }
            else if (prefixLength == bestPrefix && bestDistance is not null && distance < bestDistance)
            {
                bestDistance = distance;
            }
        }

        if (bestPrefix < 0 || bestDistance is null)
        {
            return LookupResult.Empty;
        }

        List<ResolvedRoute> selected = tableRoutes
            .Where(r =>
            {
                int prefixLength = RouteResolutionPrefixMatcher.PrefixLength(family, r.DstPrefix);
                return prefixLength == bestPrefix
                       && RouteResolutionPrefixMatcher.Contains(family, r.DstPrefix, destination)
                       && (r.Distance ?? 1) == bestDistance;
            })
            .OrderBy(static r => r.Gateway, StringComparer.Ordinal)
            .ToList();

        string? matchedPrefix = selected.Count > 0 ? selected[0].DstPrefix : null;
        RouteCandidate[] candidates = tableRoutes
            .Select(r => new RouteCandidate
            {
                DstPrefix = r.DstPrefix,
                Table = r.Table,
                Gateway = r.Gateway,
                Distance = r.Distance,
                Scope = r.Scope,
                TargetScope = r.TargetScope,
                Active = r.Active,
                Selected = selected.Any(s => SameRoute(s, r)),
                RouteKind = r.RouteKind,
            })
            .OrderBy(static c => c.DstPrefix, StringComparer.Ordinal)
            .ThenBy(static c => c.Gateway, StringComparer.Ordinal)
            .ToArray();

        return new LookupResult
        {
            MatchedPrefix = matchedPrefix,
            Candidates = candidates,
            Selected = selected,
        };
    }

    private static RecursiveOutcome ResolveRecursive(
        string family,
        string table,
        IReadOnlyList<ResolvedRoute> routes,
        IReadOnlyList<ResolvedRoute> selected)
    {
        if (selected.Count > 1)
        {
            List<ImmediateNextHop> ecmpHops = [];
            List<EcmpRouteSetBuilder.Member> ecmpMembers = [];
            HashSet<string> interfaces = new(StringComparer.Ordinal);
            List<string> executionPaths = [];
            foreach (ResolvedRoute route in selected)
            {
                RecursiveOutcome single = ResolveSingleRoute(family, table, routes, route);
                ImmediateNextHop resolvedHop = single.ImmediateNextHops.Length > 0
                    ? single.ImmediateNextHops[0]
                    : new ImmediateNextHop { Gateway = route.Gateway, Interface = route.Gateway };
                ecmpMembers.Add(new EcmpRouteSetBuilder.Member(
                    route.Active,
                    IsHardwareOffloaded(route.HwOffloaded),
                    resolvedHop));
                ecmpHops.AddRange(single.ImmediateNextHops);
                foreach (string iface in single.EgressInterfaces)
                {
                    interfaces.Add(iface);
                }

                executionPaths.Add(single.ExecutionPath ?? RouteResolutionExecutionPaths.Cpu);
            }

            return new RecursiveOutcome
            {
                Steps = selected.SelectMany(static (_, i) => Array.Empty<RecursiveResolutionStep>()).ToArray(),
                ImmediateNextHops = ecmpHops
                    .Select(h => new ImmediateNextHop
                    {
                        Gateway = h.Gateway,
                        Interface = h.Interface,
                        Selector = ImmediateNextHopSelectors.OneOf,
                    })
                    .OrderBy(static h => h.Gateway ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(static h => h.Interface ?? string.Empty, StringComparer.Ordinal)
                    .ToArray(),
                EcmpMembers = ecmpMembers.ToArray(),
                EgressInterfaces = interfaces.OrderBy(static i => i, StringComparer.Ordinal).ToArray(),
                ExecutionPath = CombineExecutionPaths(executionPaths),
                Certainty = RouteResolutionCertainties.Indeterminate,
                Decision = RouteResolutionDecisions.Forward,
            };
        }

        return ResolveSingleRoute(family, table, routes, selected[0]);
    }

    private static RecursiveOutcome ResolveSingleRoute(
        string family,
        string table,
        IReadOnlyList<ResolvedRoute> routes,
        ResolvedRoute route)
    {
        if (IsLocalGateway(route.Gateway))
        {
            ImmediateNextHop hop = new()
            {
                Gateway = route.Gateway,
                Interface = route.Gateway,
            };
            return new RecursiveOutcome
            {
                Steps = [],
                ImmediateNextHops = [hop],
                EgressInterfaces = [route.Gateway],
                ExecutionPath = ClassifyExecutionPath([route]),
                Certainty = RouteResolutionCertainties.Definite,
                Decision = RouteResolutionDecisions.LocalDelivery,
            };
        }

        string routeKind = route.RouteKind ?? string.Empty;
        if (routeKind.Equals("blackhole", StringComparison.OrdinalIgnoreCase))
        {
            return TerminalOutcome(RouteResolutionDecisions.Blackhole, route);
        }

        if (routeKind.Equals("prohibit", StringComparison.OrdinalIgnoreCase))
        {
            return TerminalOutcome(RouteResolutionDecisions.Prohibit, route);
        }

        if (routeKind.Equals("unreachable", StringComparison.OrdinalIgnoreCase))
        {
            return TerminalOutcome(RouteResolutionDecisions.Unreachable, route);
        }

        if (!string.IsNullOrWhiteSpace(route.ImmediateGateway))
        {
            (string? gateway, string? iface) = ParseImmediateGateway(route.ImmediateGateway);
            ImmediateNextHop hop = new()
            {
                Gateway = gateway,
                Interface = iface,
            };
            return new RecursiveOutcome
            {
                Steps = [],
                ImmediateNextHops = [hop],
                EgressInterfaces = iface is null ? [] : [iface],
                ExecutionPath = ClassifyExecutionPath([route]),
                Certainty = RouteResolutionCertainties.Definite,
                Decision = RouteResolutionDecisions.Forward,
            };
        }

        if (!RouteResolutionPrefixMatcher.TryParseAddress(family, route.Gateway, out UInt128 gatewayNumeric))
        {
            ImmediateNextHop hop = new() { Interface = route.Gateway, Gateway = route.Gateway };
            return new RecursiveOutcome
            {
                Steps = [],
                ImmediateNextHops = [hop],
                EgressInterfaces = [route.Gateway],
                ExecutionPath = ClassifyExecutionPath([route]),
                Certainty = RouteResolutionCertainties.Definite,
                Decision = RouteResolutionDecisions.Forward,
            };
        }

        List<RecursiveResolutionStep> steps = [];
        string resolvingTable = table;
        UInt128 currentTarget = gatewayNumeric;
        int guard = 0;
        while (guard++ < 16)
        {
            LookupResult gatewayLookup = SelectRoutes(
                family,
                resolvingTable,
                routes,
                currentTarget,
                route.Scope,
                route.TargetScope);
            if (gatewayLookup.Selected.Count == 0)
            {
                return new RecursiveOutcome
                {
                    Steps = steps,
                    ImmediateNextHops = [],
                    EgressInterfaces = [],
                    ExecutionPath = RouteResolutionExecutionPaths.Indeterminate,
                    Certainty = RouteResolutionCertainties.Indeterminate,
                    Decision = RouteResolutionDecisions.Indeterminate,
                };
            }

            ResolvedRoute resolving = gatewayLookup.Selected[0];
            (string? nextHop, string? iface) = ParseImmediateGateway(resolving.ImmediateGateway);
            nextHop ??= resolving.Gateway;
            steps.Add(new RecursiveResolutionStep
            {
                Table = resolvingTable,
                Target = FormatNumeric(family, currentTarget),
                ResolvingPrefix = gatewayLookup.MatchedPrefix ?? resolving.DstPrefix,
                Scope = resolving.Scope,
                TargetScope = resolving.TargetScope,
                NextHop = nextHop,
                Interface = iface ?? (IsLocalGateway(resolving.Gateway) ? resolving.Gateway : null),
                Active = resolving.Active,
            });

            if (!string.IsNullOrWhiteSpace(resolving.ImmediateGateway) || IsLocalGateway(resolving.Gateway))
            {
                ImmediateNextHop hop = new()
                {
                    Gateway = nextHop,
                    Interface = iface ?? (IsLocalGateway(resolving.Gateway) ? resolving.Gateway : null),
                };
                return new RecursiveOutcome
                {
                    Steps = steps,
                    ImmediateNextHops = [hop],
                    EgressInterfaces = hop.Interface is null ? [] : [hop.Interface],
                    ExecutionPath = ClassifyExecutionPath([route, resolving]),
                    Certainty = RouteResolutionCertainties.Definite,
                    Decision = RouteResolutionDecisions.Forward,
                };
            }

            if (!RouteResolutionPrefixMatcher.TryParseAddress(family, resolving.Gateway, out UInt128 nextTarget))
            {
                break;
            }

            if (nextTarget == currentTarget)
            {
                return new RecursiveOutcome
                {
                    Steps = steps,
                    ImmediateNextHops = [],
                    EgressInterfaces = [],
                    ExecutionPath = RouteResolutionExecutionPaths.Indeterminate,
                    Certainty = RouteResolutionCertainties.Indeterminate,
                    Decision = RouteResolutionDecisions.Indeterminate,
                };
            }

            currentTarget = nextTarget;
        }

        return new RecursiveOutcome
        {
            Steps = steps,
            ImmediateNextHops = [],
            EgressInterfaces = [],
            ExecutionPath = RouteResolutionExecutionPaths.Indeterminate,
            Certainty = RouteResolutionCertainties.Indeterminate,
            Decision = RouteResolutionDecisions.Indeterminate,
        };
    }

    private static LookupResult SelectRoutes(
        string family,
        string table,
        IReadOnlyList<ResolvedRoute> routes,
        UInt128 destination,
        int? requiredScope,
        int? targetScope)
    {
        LookupResult lookup = SelectRoutes(family, table, routes, destination);
        if (lookup.Selected.Count == 0)
        {
            return lookup;
        }

        List<ResolvedRoute> scoped = lookup.Selected
            .Where(r => ScopeMatches(requiredScope, targetScope, r.Scope))
            .ToList();
        return scoped.Count == 0
            ? LookupResult.Empty
            : lookup with { Selected = scoped };
    }

    private static bool ScopeMatches(int? requiredScope, int? targetScope, int? routeScope)
    {
        if (targetScope is null)
        {
            return true;
        }

        int scope = routeScope ?? 0;
        return scope <= targetScope;
    }

    private static RecursiveOutcome TerminalOutcome(string decision, ResolvedRoute route)
        => new()
        {
            Steps = [],
            ImmediateNextHops = [],
            EgressInterfaces = [],
            ExecutionPath = ClassifyExecutionPath([route]),
            Certainty = RouteResolutionCertainties.Definite,
            Decision = decision,
        };

    private static RouteResolutionTrace BuildTrace(
        RouteResolutionQuery query,
        IReadOnlyList<string> decisionOrder,
        string effectiveVrf,
        PolicyOutcome policy,
        IReadOnlyList<ResolvedRoute> routes,
        UInt128 destination)
    {
        string decision = policy.Decision ?? RouteResolutionDecisions.NoRoute;
        IReadOnlyList<SelectedRoute> selectedRoutes = policy.Selected
            .Select(r => new SelectedRoute
            {
                DstPrefix = r.DstPrefix,
                Table = r.Table,
                Gateway = r.Gateway,
                Distance = r.Distance,
                ImmediateGateway = r.ImmediateGateway,
                RouteKind = r.RouteKind,
            })
            .ToArray();

        if (decision == RouteResolutionDecisions.NoRoute
            && policy.Candidates.Count == 0
            && string.IsNullOrWhiteSpace(policy.SelectedTable))
        {
            policy.SelectedTable = "main";
        }

        return new RouteResolutionTrace
        {
            Family = query.Family,
            SourceAddress = query.SourceAddress,
            DestinationAddress = query.DestinationAddress,
            IngressInterface = query.IngressInterface,
            InitialVrf = query.InitialVrf ?? effectiveVrf,
            RoutingMark = policy.RoutingMark,
            RoutingDecisionOrder = decisionOrder,
            MatchedMangleRule = policy.MatchedMangleRule,
            MatchedRoutingRule = policy.MatchedRoutingRule,
            RoutingRuleAction = policy.RoutingRuleAction,
            SelectedVrf = policy.SelectedVrf,
            SelectedTable = policy.SelectedTable,
            MatchedPrefix = policy.MatchedPrefix,
            RouteCandidates = policy.Candidates,
            SelectedRoutes = selectedRoutes,
            RecursiveResolution = policy.RecursiveSteps,
            ImmediateNextHops = policy.ImmediateNextHops,
            EcmpRouteSet = EcmpRouteSetBuilder.Build(
                query,
                policy.SelectedTable,
                policy.MatchedPrefix,
                decision,
                policy.EcmpMembers),
            EgressInterfaces = policy.EgressInterfaces,
            PreferredSource = policy.PreferredSource,
            Decision = decision,
            ExecutionPath = policy.ExecutionPath ?? RouteResolutionExecutionPaths.Indeterminate,
            Certainty = policy.Certainty ?? RouteResolutionCertainties.Definite,
        };
    }

    private static ResolvedRoute[] BuildRouteIndex(
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational,
        string family)
    {
        Dictionary<string, ResolvedRoute> index = new(StringComparer.Ordinal);
        foreach (StaticRouteConfigFact config in configuration.StaticRoutes)
        {
            if (!string.Equals(config.Family, family, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string gateway = config.Gateway ?? string.Empty;
            string table = config.RoutingTable ?? "main";
            string dst = config.DstAddress ?? "0.0.0.0/0";
            string key = RouteKey(table, dst, gateway);
            RouteObservationFact? observation = operational.Routes.FirstOrDefault(r =>
                string.Equals(r.RoutingTable, table, StringComparison.Ordinal)
                && string.Equals(r.DstAddress, dst, StringComparison.Ordinal)
                && string.Equals(r.Gateway, gateway, StringComparison.Ordinal));
            index[key] = new ResolvedRoute
            {
                Family = family,
                Table = table,
                DstPrefix = dst,
                Gateway = gateway,
                Distance = config.Distance,
                Scope = config.Scope,
                TargetScope = config.TargetScope,
                PreferredSource = config.PrefSrc,
                Disabled = config.Disabled,
                Active = observation is null ? !IsDisabled(config.Disabled) : IsActive(observation.Active),
                ImmediateGateway = observation?.ImmediateGateway,
                HwOffloaded = observation?.HwOffloaded,
                RouteKind = ClassifyRouteKind(gateway),
            };
        }

        foreach (RouteObservationFact observation in operational.Routes)
        {
            if (!string.Equals(observation.Family, family, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string table = observation.RoutingTable ?? "main";
            string dst = observation.DstAddress ?? "0.0.0.0/0";
            string gateway = observation.Gateway ?? string.Empty;
            string key = RouteKey(table, dst, gateway);
            if (index.TryGetValue(key, out ResolvedRoute? existing))
            {
                index[key] = existing with
                {
                    Active = IsActive(observation.Active),
                    ImmediateGateway = observation.ImmediateGateway ?? existing.ImmediateGateway,
                    HwOffloaded = observation.HwOffloaded ?? existing.HwOffloaded,
                };
                continue;
            }

            index[key] = new ResolvedRoute
            {
                Family = family,
                Table = table,
                DstPrefix = dst,
                Gateway = gateway,
                Distance = null,
                Scope = null,
                TargetScope = null,
                PreferredSource = null,
                Disabled = "false",
                Active = IsActive(observation.Active),
                ImmediateGateway = observation.ImmediateGateway,
                HwOffloaded = observation.HwOffloaded,
                RouteKind = ClassifyRouteKind(gateway),
            };
        }

        return index.Values.OrderBy(static r => r.Table, StringComparer.Ordinal)
            .ThenBy(static r => r.DstPrefix, StringComparer.Ordinal)
            .ThenBy(static r => r.Gateway, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ClassifyRouteKind(string gateway)
    {
        if (gateway.Equals("blackhole", StringComparison.OrdinalIgnoreCase))
        {
            return "blackhole";
        }

        if (gateway.Equals("prohibit", StringComparison.OrdinalIgnoreCase))
        {
            return "prohibit";
        }

        if (gateway.Equals("unreachable", StringComparison.OrdinalIgnoreCase))
        {
            return "unreachable";
        }

        return IsLocalGateway(gateway) ? "connected" : "unicast";
    }

    private static string ClassifyRouteDecision(ResolvedRoute route)
    {
        return (route.RouteKind ?? string.Empty).ToLowerInvariant() switch
        {
            "blackhole" => RouteResolutionDecisions.Blackhole,
            "prohibit" => RouteResolutionDecisions.Prohibit,
            "unreachable" => RouteResolutionDecisions.Unreachable,
            "connected" => RouteResolutionDecisions.LocalDelivery,
            _ => RouteResolutionDecisions.Forward,
        };
    }

    private static ImmediateNextHop[] BuildImmediateNextHops(
        IReadOnlyList<ResolvedRoute> selected,
        string decision)
    {
        if (decision is RouteResolutionDecisions.Blackhole
            or RouteResolutionDecisions.Prohibit
            or RouteResolutionDecisions.Unreachable
            or RouteResolutionDecisions.NoRoute)
        {
            return [];
        }

        if (selected.Count > 1)
        {
            return selected
                .Select(r =>
                {
                    (string? gateway, string? iface) = ParseImmediateGateway(r.ImmediateGateway);
                    return new ImmediateNextHop
                    {
                        Gateway = gateway ?? r.Gateway,
                        Interface = iface,
                        Selector = ImmediateNextHopSelectors.OneOf,
                    };
                })
                .ToArray();
        }

        ResolvedRoute route = selected[0];
        (string? singleGateway, string? singleIface) = ParseImmediateGateway(route.ImmediateGateway);
        return
        [
            new ImmediateNextHop
            {
                Gateway = singleGateway ?? route.Gateway,
                Interface = singleIface ?? (IsLocalGateway(route.Gateway) ? route.Gateway : null),
            },
        ];
    }

    private static string[] ExtractInterfaces(IReadOnlyList<ImmediateNextHop> hops)
        => hops.Select(static h => h.Interface)
            .Where(static i => !string.IsNullOrWhiteSpace(i))
            .Select(static i => i!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static i => i, StringComparer.Ordinal)
            .ToArray();

    private static string? ClassifyExecutionPath(IReadOnlyList<ResolvedRoute> routes)
    {
        bool anyHw = false;
        bool anyCpu = false;
        foreach (ResolvedRoute route in routes)
        {
            if (IsHardwareOffloaded(route.HwOffloaded))
            {
                anyHw = true;
            }
            else
            {
                anyCpu = true;
            }
        }

        if (anyHw && anyCpu)
        {
            return RouteResolutionExecutionPaths.Mixed;
        }

        if (anyHw)
        {
            return RouteResolutionExecutionPaths.Hardware;
        }

        if (anyCpu)
        {
            return RouteResolutionExecutionPaths.Cpu;
        }

        return RouteResolutionExecutionPaths.Indeterminate;
    }

    private static string CombineExecutionPaths(IReadOnlyList<string> paths)
    {
        bool anyHw = paths.Any(static p => p == RouteResolutionExecutionPaths.Hardware);
        bool anyCpu = paths.Any(static p => p == RouteResolutionExecutionPaths.Cpu);
        bool anyMixed = paths.Any(static p => p == RouteResolutionExecutionPaths.Mixed);
        if (anyMixed || (anyHw && anyCpu))
        {
            return RouteResolutionExecutionPaths.Mixed;
        }

        if (anyHw)
        {
            return RouteResolutionExecutionPaths.Hardware;
        }

        if (anyCpu)
        {
            return RouteResolutionExecutionPaths.Cpu;
        }

        return RouteResolutionExecutionPaths.Indeterminate;
    }

    private static bool MatchesRoutingRule(
        RoutingRuleFact rule,
        RouteResolutionQuery query,
        string? effectiveMark,
        UInt128 destination,
        UInt128 source)
    {
        if (!RouteResolutionPrefixMatcher.MatchesSelector(query.Family, rule.DstAddress, destination))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.SrcAddress)
            && !RouteResolutionPrefixMatcher.MatchesSelector(query.Family, rule.SrcAddress, source))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.RoutingMark)
            && !string.Equals(rule.RoutingMark.Trim(), effectiveMark?.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool TableExists(RoutingConfigurationSnapshot configuration, string table)
        => configuration.Tables.Any(t => string.Equals(t.Name, table, StringComparison.Ordinal))
           || configuration.Rules.Any(r => string.Equals(r.Table, table, StringComparison.Ordinal))
           || configuration.StaticRoutes.Any(r => string.Equals(r.RoutingTable, table, StringComparison.Ordinal));

    private static bool SameRoute(ResolvedRoute left, ResolvedRoute right)
        => string.Equals(left.Table, right.Table, StringComparison.Ordinal)
           && string.Equals(left.DstPrefix, right.DstPrefix, StringComparison.Ordinal)
           && string.Equals(left.Gateway, right.Gateway, StringComparison.Ordinal);

    private static string RouteKey(string table, string dst, string gateway)
        => string.Create(CultureInfo.InvariantCulture, $"{table}|{dst}|{gateway}");

    private static bool IsDisabled(string? disabled)
        => string.Equals(disabled?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool IsActive(string? active)
        => !string.Equals(active?.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    private static bool IsHardwareOffloaded(string? hwOffloaded)
        => string.Equals(hwOffloaded?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalGateway(string gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            return false;
        }

        return !gateway.Contains('.', StringComparison.Ordinal)
               && !gateway.Contains(':', StringComparison.Ordinal);
    }

    private static (string? Gateway, string? Interface) ParseImmediateGateway(string? immediateGateway)
    {
        if (string.IsNullOrWhiteSpace(immediateGateway))
        {
            return (null, null);
        }

        int percent = immediateGateway.IndexOf('%');
        if (percent <= 0)
        {
            return (immediateGateway, null);
        }

        return (immediateGateway[..percent], immediateGateway[(percent + 1)..]);
    }

    private static string FormatNumeric(string family, UInt128 value)
    {
        int width = RouteResolutionPrefixMatcher.TryParseFamily(family, out IpAddressFamily parsed) && parsed == IpAddressFamily.IPv6 ? 128 : 32;
        if (width == 32)
        {
            uint v = (uint)value;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{v >> 24}.{(v >> 16) & 0xFF}.{(v >> 8) & 0xFF}.{v & 0xFF}");
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return RoutingRuleActions.Lookup;
        }

        return action.Trim().Replace('-', '_').ToUpperInvariant() switch
        {
            "LOOKUP" => RoutingRuleActions.Lookup,
            "LOOKUP_ONLY" => RoutingRuleActions.LookupOnly,
            "DROP" => RoutingRuleActions.Drop,
            "UNREACHABLE" => RoutingRuleActions.Unreachable,
            _ => action.Trim().ToUpperInvariant(),
        };
    }

    private sealed class PolicyOutcome
    {
        public string? SelectedVrf { get; set; }

        public string? SelectedTable { get; set; }

        public string? RoutingMark { get; set; }

        public MatchedMangleRule? MatchedMangleRule { get; set; }

        public MatchedRoutingRule? MatchedRoutingRule { get; set; }

        public string? RoutingRuleAction { get; set; }

        public string? MatchedPrefix { get; set; }

        public IReadOnlyList<RouteCandidate> Candidates { get; set; } = [];

        public IReadOnlyList<ResolvedRoute> Selected { get; set; } = [];

        public IReadOnlyList<RecursiveResolutionStep> RecursiveSteps { get; set; } = [];

        public IReadOnlyList<ImmediateNextHop> ImmediateNextHops { get; set; } = [];

        public IReadOnlyList<string> EgressInterfaces { get; set; } = [];

        public string? PreferredSource { get; set; }

        public string? Decision { get; set; }

        public string? ExecutionPath { get; set; }

        public string? Certainty { get; set; }

        public IReadOnlyList<EcmpRouteSetBuilder.Member> EcmpMembers { get; set; } = [];
    }

    private sealed record ResolvedRoute
    {
        public required string Family { get; init; }

        public required string Table { get; init; }

        public required string DstPrefix { get; init; }

        public required string Gateway { get; init; }

        public int? Distance { get; init; }

        public int? Scope { get; init; }

        public int? TargetScope { get; init; }

        public string? PreferredSource { get; init; }

        public string? Disabled { get; init; }

        public bool Active { get; init; }

        public string? ImmediateGateway { get; init; }

        public string? HwOffloaded { get; init; }

        public string? RouteKind { get; init; }
    }

    private sealed record LookupResult
    {
        public static LookupResult Empty { get; } = new()
        {
            MatchedPrefix = null,
            Candidates = [],
            Selected = [],
        };

        public string? MatchedPrefix { get; init; }

        public IReadOnlyList<RouteCandidate> Candidates { get; init; } = [];

        public List<ResolvedRoute> Selected { get; init; } = [];
    }

    private sealed class RecursiveOutcome
    {
        public IReadOnlyList<RecursiveResolutionStep> Steps { get; init; } = [];

        public ImmediateNextHop[] ImmediateNextHops { get; init; } = [];

        public IReadOnlyList<string> EgressInterfaces { get; init; } = [];

        public string? ExecutionPath { get; init; }

        public string? Certainty { get; init; }

        public string? Decision { get; init; }

        public EcmpRouteSetBuilder.Member[] EcmpMembers { get; init; } = [];
    }
}
