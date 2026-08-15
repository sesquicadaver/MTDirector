using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Structural validation and predicate satisfiability (Policy Model §38–§39 / M2-10).
/// Does not treat <see cref="PredicateAlgebra.Relate"/> as exact packet-space truth:
/// emptiness is interval resolve, zone include−exclude, service union, and cube drop after
/// <see cref="PredicateNormalizer"/> — not EQUAL-on-INDETERMINATE.
/// </summary>
public static class PolicyAnalysisEngine
{
    /// <summary>
    /// Analyzes every rule, including disabled ones. Sequence analysis runs only when
    /// there are no blockers.
    /// </summary>
    public static PolicyAnalysisResult Analyze(
        IReadOnlyList<PolicyRule> rules,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services,
        IReadOnlySet<Guid> knownZoneIds,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? extraMatchersByRuleId = null,
        PolicySequenceAnalyzer? sequenceAnalyzer = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(knownZoneIds);

        List<PolicyAnalysisFinding> findings = [];
        foreach (PolicyRule rule in rules)
        {
            IReadOnlyList<string> extra = extraMatchersByRuleId is not null
                && extraMatchersByRuleId.TryGetValue(rule.Id.Value, out IReadOnlyList<string>? keys)
                ? keys
                : [];
            CollectRuleFindings(rule, addresses, services, knownZoneIds, extra, findings);
        }

        if (findings.Exists(static f => f.Severity == PolicyAnalysisCodes.SeverityBlocker))
        {
            return new PolicyAnalysisResult
            {
                Findings = findings,
                SequenceAnalyzerInvoked = false,
            };
        }

        if (sequenceAnalyzer is null)
        {
            return new PolicyAnalysisResult
            {
                Findings = findings,
                SequenceAnalyzerInvoked = false,
            };
        }

        IReadOnlyList<PolicyAnalysisFinding> sequenceFindings = sequenceAnalyzer(rules);
        ArgumentNullException.ThrowIfNull(sequenceFindings);
        findings.AddRange(sequenceFindings);
        return new PolicyAnalysisResult
        {
            Findings = findings,
            SequenceAnalyzerInvoked = true,
        };
    }

    /// <summary>
    /// INPUT forbids egress zones; OUTPUT forbids ingress zones (Policy Model §22).
    /// Exposed for rules that cannot be constructed via <see cref="PolicyRule.Create"/>.
    /// </summary>
    public static PolicyAnalysisFinding? TryZoneDirection(
        Guid ruleId,
        PolicyFilterChain chain,
        TrafficPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return chain switch
        {
            PolicyFilterChain.Input when predicate.EgressZones is not null => Blocker(
                PolicyAnalysisCodes.ZoneDirection,
                ruleId,
                $"Rule '{ruleId:D}' on INPUT forbids egress zone selectors."),
            PolicyFilterChain.Output when predicate.IngressZones is not null => Blocker(
                PolicyAnalysisCodes.ZoneDirection,
                ruleId,
                $"Rule '{ruleId:D}' on OUTPUT forbids ingress zone selectors."),
            _ => null,
        };
    }

    private static void CollectRuleFindings(
        PolicyRule rule,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services,
        IReadOnlySet<Guid> knownZoneIds,
        IReadOnlyList<string> extraMatchers,
        List<PolicyAnalysisFinding> findings)
    {
        Guid id = rule.Id.Value;
        foreach (string matcher in extraMatchers)
        {
            if (string.IsNullOrWhiteSpace(matcher))
            {
                continue;
            }

            findings.Add(Blocker(
                PolicyAnalysisCodes.UnsupportedMatcher,
                id,
                $"Rule '{id:D}' uses unsupported matcher '{matcher}'."));
        }

        PolicyAnalysisFinding? zoneDirection = TryZoneDirection(id, rule.Chain, rule.Predicate);
        if (zoneDirection is not null)
        {
            findings.Add(zoneDirection);
        }

        if (HasConnectionStateContradiction(rule.Predicate.ConnectionStates))
        {
            findings.Add(Blocker(
                PolicyAnalysisCodes.ConnectionState,
                id,
                $"Rule '{id:D}' has a contradictory connection-state combination."));
        }

        PolicyAnalysisFinding? ipsec = TryIpsecDirection(id, rule.Chain, rule.Predicate.IpsecPolicy);
        if (ipsec is not null)
        {
            findings.Add(ipsec);
        }

        if (HasFamilyImpossibleAddressType(rule.Family, rule.Predicate.SourceAddressTypes)
            || HasFamilyImpossibleAddressType(rule.Family, rule.Predicate.DestinationAddressTypes))
        {
            findings.Add(Blocker(
                PolicyAnalysisCodes.Unsatisfiable,
                id,
                $"Rule '{id:D}' address types are empty in family {rule.Family}."));
        }

        ServiceSelectorResolveResult? resolvedServices = null;
        if (rule.Predicate.Services is not null)
        {
            try
            {
                resolvedServices = ServiceSelectorResolver.Resolve(
                    rule.Predicate.Services,
                    rule.Family,
                    services);
            }
            catch (DomainInvariantException ex)
            {
                findings.Add(MapServiceResolveFailure(id, ex.Message));
            }
        }

        if (resolvedServices is { IsAnyProtocol: false, Terms.Count: 0 })
        {
            findings.Add(Blocker(
                PolicyAnalysisCodes.Unsatisfiable,
                id,
                $"Rule '{id:D}' service union is empty."));
        }

        if (rule.Predicate.TcpFlags is not null
            && resolvedServices is { IsAnyProtocol: false }
            && !HasTcpTerm(resolvedServices.Terms))
        {
            findings.Add(Blocker(
                PolicyAnalysisCodes.TcpFlagsProtocol,
                id,
                $"Rule '{id:D}' TCP flags require a TCP service."));
        }

        if (rule.Effect.Kind == PolicyRuleEffect.Reject
            && rule.Effect.RejectModeValue == RejectMode.TcpReset
            && !IsTcpOnly(rule.Predicate, resolvedServices))
        {
            findings.Add(Blocker(
                PolicyAnalysisCodes.TcpFlagsProtocol,
                id,
                $"Rule '{id:D}' TCP_RESET requires a TCP-only traffic predicate."));
        }

        TryAddressSatisfiability(id, rule.Family, rule.Predicate.SourceAddresses, addresses, "source", findings);
        TryAddressSatisfiability(id, rule.Family, rule.Predicate.DestinationAddresses, addresses, "destination", findings);
        TryZoneSatisfiability(id, rule.Predicate.IngressZones, knownZoneIds, "ingress", findings);
        TryZoneSatisfiability(id, rule.Predicate.EgressZones, knownZoneIds, "egress", findings);

        if (findings.Exists(f => f.RuleId == id && f.Severity == PolicyAnalysisCodes.SeverityBlocker))
        {
            return;
        }

        PredicateAlgebraResult normalized = PredicateNormalizer.Normalize(
            rule.Predicate,
            rule.Family,
            rule.Chain,
            addresses,
            services);
        if (normalized.IsFailure)
        {
            string code = normalized.Code == PredicateAlgebraCodes.ComplexityLimit
                ? PredicateAlgebraCodes.ComplexityLimit
                : MapNormalizeFailureCode(normalized.Message ?? string.Empty);
            findings.Add(Blocker(
                code,
                id,
                normalized.Message ?? $"Rule '{id:D}' predicate could not be normalized."));
            return;
        }

        if (normalized.Value is { IsEmpty: true })
        {
            findings.Add(Blocker(
                PolicyAnalysisCodes.Unsatisfiable,
                id,
                $"Rule '{id:D}' normalized predicate is empty."));
        }
    }

    private static void TryAddressSatisfiability(
        Guid ruleId,
        IpAddressFamily family,
        AddressSelector? selector,
        IReadOnlyDictionary<AddressObjectId, AddressObject> catalog,
        string label,
        List<PolicyAnalysisFinding> findings)
    {
        if (selector is null)
        {
            return;
        }

        try
        {
            AddressSelectorResolveResult resolved = AddressSelectorResolver.Resolve(selector, family, catalog);
            if (resolved.IsUnsatisfiable)
            {
                findings.Add(Blocker(
                    PolicyAnalysisCodes.Unsatisfiable,
                    ruleId,
                    $"Rule '{ruleId:D}' {label} address selector is empty."));
            }
        }
        catch (DomainInvariantException ex)
        {
            findings.Add(Blocker(
                PolicyComposeCodes.SelectorUnresolved,
                ruleId,
                ex.Message));
        }
    }

    private static void TryZoneSatisfiability(
        Guid ruleId,
        ZoneSelector? selector,
        IReadOnlySet<Guid> knownZoneIds,
        string label,
        List<PolicyAnalysisFinding> findings)
    {
        if (selector is null)
        {
            return;
        }

        HashSet<Guid> remaining;
        if (selector.Include.Count == 0)
        {
            if (knownZoneIds.Count == 0)
            {
                return;
            }

            remaining = [.. knownZoneIds];
        }
        else
        {
            remaining = selector.Include.Select(static z => z.Value).ToHashSet();
        }

        remaining.ExceptWith(selector.Exclude.Select(static z => z.Value));
        if (remaining.Count == 0)
        {
            findings.Add(Blocker(
                PolicyAnalysisCodes.Unsatisfiable,
                ruleId,
                $"Rule '{ruleId:D}' {label} zone selector is empty."));
        }
    }

    private static PolicyAnalysisFinding? TryIpsecDirection(
        Guid ruleId,
        PolicyFilterChain chain,
        IpsecPolicyPredicate? ipsec)
    {
        if (ipsec is null)
        {
            return null;
        }

        if (chain == PolicyFilterChain.Input && ipsec.Direction == IpsecDirection.Out)
        {
            return Blocker(
                PolicyAnalysisCodes.IpsecDirection,
                ruleId,
                $"Rule '{ruleId:D}' on INPUT cannot use IPsec direction OUT.");
        }

        if (chain == PolicyFilterChain.Output && ipsec.Direction == IpsecDirection.In)
        {
            return Blocker(
                PolicyAnalysisCodes.IpsecDirection,
                ruleId,
                $"Rule '{ruleId:D}' on OUTPUT cannot use IPsec direction IN.");
        }

        return null;
    }

    private static bool HasConnectionStateContradiction(IReadOnlyList<ConnectionState>? states)
    {
        if (states is null || states.Count <= 1)
        {
            return false;
        }

        bool invalid = states.Contains(ConnectionState.Invalid);
        bool untracked = states.Contains(ConnectionState.Untracked);
        bool tracked = states.Any(static s =>
            s is ConnectionState.New or ConnectionState.Established or ConnectionState.Related);
        return (invalid && tracked) || (untracked && (tracked || invalid));
    }

    private static bool HasFamilyImpossibleAddressType(
        IpAddressFamily family,
        IReadOnlyList<AddressType>? types)
        => family == IpAddressFamily.IPv6
           && types is not null
           && types.Contains(AddressType.Broadcast);

    private static bool HasTcpTerm(IReadOnlyList<ServiceTerm> terms)
        => terms.Any(static t => !t.Protocol.IsAny && t.Protocol.Number == IpProtocol.Tcp);

    private static bool IsTcpOnly(TrafficPredicate predicate, ServiceSelectorResolveResult? resolved)
    {
        if (predicate.IsTcpOnly())
        {
            return true;
        }

        return resolved is { IsAnyProtocol: false, Terms.Count: > 0 }
               && resolved.Terms.All(static t => t.Protocol.Number == IpProtocol.Tcp);
    }

    private static PolicyAnalysisFinding MapServiceResolveFailure(Guid ruleId, string message)
    {
        bool icmp = message.Contains("ICMP", StringComparison.OrdinalIgnoreCase);
        return Blocker(
            icmp ? PolicyAnalysisCodes.IcmpFamily : PolicyAnalysisCodes.Unsatisfiable,
            ruleId,
            message);
    }

    private static string MapNormalizeFailureCode(string message)
        => message.Contains("ICMP", StringComparison.OrdinalIgnoreCase)
            ? PolicyAnalysisCodes.IcmpFamily
            : PolicyComposeCodes.SelectorUnresolved;

    private static PolicyAnalysisFinding Blocker(string code, Guid ruleId, string message)
        => new()
        {
            Code = code,
            Severity = PolicyAnalysisCodes.SeverityBlocker,
            RuleId = ruleId,
            Message = message,
        };
}
