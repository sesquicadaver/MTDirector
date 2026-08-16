using System.Globalization;
using System.Net;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Evaluates PolicyTestCase packets against managed rules and optional actual-filter context (M2-16).
/// Does not write RouterOS. SYSTEM tests cannot be skipped.
/// </summary>
public static class PolicyTestEvaluator
{
    private enum Match : byte
    {
        Miss = 0,
        Hit = 1,
        Indeterminate = 2,
    }

    private static readonly HashSet<string> ProvenActualMatchers = new(StringComparer.Ordinal)
    {
        "protocol",
        "src-address",
        "dst-address",
        "connection-state",
    };

    public static IReadOnlyList<PolicyTestResult> Evaluate(
        IReadOnlyList<PolicyTestCase> tests,
        IReadOnlyList<PolicyRule> rules,
        ChainContractSet contracts,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services,
        IReadOnlyList<ActualFilterRule>? actualFilter = null)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(services);

        List<PolicyRule> ordered = rules
            .OrderBy(static r => r.Family)
            .ThenBy(static r => r.Chain)
            .ThenBy(static r => PolicyPipelineV1.Ordinal(r.Stage))
            .ThenBy(static r => r.Ordinal)
            .ThenBy(static r => r.Id.Value)
            .ToList();

        List<PolicyTestResult> results = [];
        foreach (PolicyTestCase test in tests.OrderBy(static t => t.Id.ToString(), StringComparer.Ordinal))
        {
            results.Add(EvaluateOne(test, ordered, contracts, addresses, services, actualFilter));
        }

        return results;
    }

    private static PolicyTestResult EvaluateOne(
        PolicyTestCase test,
        IReadOnlyList<PolicyRule> ordered,
        ChainContractSet contracts,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services,
        IReadOnlyList<ActualFilterRule>? actualFilter)
    {
        if (test.Origin == PolicyTestOrigin.System && !test.Enabled)
        {
            return Result(
                test,
                PolicyEvidenceAnalysisCodes.OutcomeFail,
                [],
                PolicyTestExpectedDisposition.Drop,
                PolicyEvidenceAnalysisCodes.ProofIndeterminate,
                PolicyEvidenceAnalysisCodes.SystemTestDisabled);
        }

        if (!test.Enabled)
        {
            return Result(
                test,
                PolicyEvidenceAnalysisCodes.OutcomePass,
                [],
                test.Expected,
                PolicyEvidenceAnalysisCodes.ProofProven,
                failureCode: null);
        }

        if (test.ExecutionMode == PolicyTestExecutionMode.NodeEffective && actualFilter is null)
        {
            return Result(
                test,
                PolicyEvidenceAnalysisCodes.OutcomeFail,
                [],
                test.Expected,
                PolicyEvidenceAnalysisCodes.ProofIndeterminate,
                PolicyEvidenceAnalysisCodes.NodeEffectiveIndeterminate);
        }

        List<PolicyTestPathHop> path = [];
        if (test.ExecutionMode == PolicyTestExecutionMode.NodeEffective)
        {
            Match unmanaged = TryActualFilter(
                test.Packet,
                actualFilter!,
                preAnchor: true,
                path,
                out PolicyTestExpectedDisposition? unmanagedDisposition);
            if (unmanaged == Match.Indeterminate)
            {
                return Result(
                    test,
                    PolicyEvidenceAnalysisCodes.OutcomeFail,
                    path,
                    test.Expected,
                    PolicyEvidenceAnalysisCodes.ProofIndeterminate,
                    PolicyEvidenceAnalysisCodes.NodeEffectiveIndeterminate);
            }

            if (unmanaged == Match.Hit && unmanagedDisposition is not null)
            {
                return Finish(test, path, ruleId: null, stage: null, unmanagedDisposition.Value);
            }
        }

        foreach (PolicyRule rule in ordered)
        {
            if (rule.Family != test.Packet.Family || rule.Chain != test.Packet.Chain)
            {
                continue;
            }

            if (!rule.Enabled)
            {
                continue;
            }

            Match match = MatchRule(rule, test.Packet, addresses, services);
            if (match == Match.Indeterminate)
            {
                path.Add(Hop(PolicyTestPathKind.ManagedRule, rule.Id.ToString(), rule.Stage, rule.Id));
                return Result(
                    test,
                    PolicyEvidenceAnalysisCodes.OutcomeFail,
                    path,
                    test.Expected,
                    PolicyEvidenceAnalysisCodes.ProofIndeterminate,
                    test.Origin == PolicyTestOrigin.System
                        ? PolicyEvidenceAnalysisCodes.SafetyTestFailed
                        : PolicyEvidenceAnalysisCodes.NodeEffectiveIndeterminate);
            }

            if (match == Match.Miss)
            {
                continue;
            }

            path.Add(Hop(PolicyTestPathKind.ManagedStage, PolicyPipelineV1.FormatStage(rule.Stage), rule.Stage, rule.Id));
            path.Add(Hop(PolicyTestPathKind.ManagedRule, rule.Id.ToString(), rule.Stage, rule.Id));
            if (rule.Effect.Kind == PolicyRuleEffect.ExemptDenyStage)
            {
                path.Add(Hop(PolicyTestPathKind.ExceptionReturn, rule.Id.ToString(), rule.Stage, rule.Id));
                continue;
            }

            return Finish(test, path, rule.Id, rule.Stage, MapEffect(rule.Effect.Kind));
        }

        if (test.ExecutionMode == PolicyTestExecutionMode.NodeEffective)
        {
            Match post = TryActualFilter(
                test.Packet,
                actualFilter!,
                preAnchor: false,
                path,
                out PolicyTestExpectedDisposition? postDisposition);
            if (post == Match.Indeterminate)
            {
                return Result(
                    test,
                    PolicyEvidenceAnalysisCodes.OutcomeFail,
                    path,
                    test.Expected,
                    PolicyEvidenceAnalysisCodes.ProofIndeterminate,
                    PolicyEvidenceAnalysisCodes.NodeEffectiveIndeterminate);
            }

            if (post == Match.Hit && postDisposition is not null)
            {
                return Finish(test, path, ruleId: null, stage: null, postDisposition.Value);
            }
        }

        ChainContract? contract = contracts.Find(test.Packet.Family, test.Packet.Chain);
        PolicyTestExpectedDisposition fallback = contract is null
            ? PolicyTestExpectedDisposition.Drop
            : MapDefault(contract.DefaultDisposition);
        path.Add(Hop(PolicyTestPathKind.DefaultDisposition, fallback.ToString(), PolicyPipelineStage.DefaultDisposition, null));
        return Finish(test, path, ruleId: null, PolicyPipelineStage.DefaultDisposition, fallback);
    }

    private static PolicyTestResult Finish(
        PolicyTestCase test,
        List<PolicyTestPathHop> path,
        RuleId? ruleId,
        PolicyPipelineStage? stage,
        PolicyTestExpectedDisposition actual)
    {
        bool expectedRuleOk = test.ExpectedRuleId is null || test.ExpectedRuleId == ruleId;
        bool pass = actual == test.Expected && expectedRuleOk;
        string? failure = pass
            ? null
            : test.Origin == PolicyTestOrigin.System
                ? PolicyEvidenceAnalysisCodes.SafetyTestFailed
                : null;
        return Result(
            test,
            pass ? PolicyEvidenceAnalysisCodes.OutcomePass : PolicyEvidenceAnalysisCodes.OutcomeFail,
            path,
            actual,
            PolicyEvidenceAnalysisCodes.ProofProven,
            failure,
            ruleId,
            stage);
    }

    private static Match MatchRule(
        PolicyRule rule,
        PolicyTestPacket packet,
        IReadOnlyDictionary<AddressObjectId, AddressObject> addresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> services)
    {
        PredicateAlgebraResult normalized = PredicateNormalizer.Normalize(
            rule.Predicate,
            rule.Family,
            rule.Chain,
            addresses,
            services);
        if (normalized.IsFailure || normalized.Value is null)
        {
            return Match.Indeterminate;
        }

        if (normalized.Value.IsEmpty)
        {
            return Match.Miss;
        }

        bool anyHit = false;
        foreach (AtomicTrafficCube cube in normalized.Value.Cubes)
        {
            Match one = MatchCube(cube, packet);
            if (one == Match.Indeterminate)
            {
                return Match.Indeterminate;
            }

            if (one == Match.Hit)
            {
                anyHit = true;
            }
        }

        return anyHit ? Match.Hit : Match.Miss;
    }

    private static Match MatchCube(AtomicTrafficCube cube, PolicyTestPacket packet)
    {
        if (cube.Family != packet.Family || cube.Chain != packet.Chain)
        {
            return Match.Miss;
        }

        if (!ContainsAddress(cube.SourceAddresses, packet.Family, packet.SourceAddress)
            || !ContainsAddress(cube.DestinationAddresses, packet.Family, packet.DestinationAddress))
        {
            return Match.Miss;
        }

        Match zones = ContainsOptional(cube.IngressZones, packet.IngressZoneId);
        if (zones != Match.Hit)
        {
            return zones == Match.Miss ? Match.Miss : Match.Indeterminate;
        }

        zones = ContainsOptional(cube.EgressZones, packet.EgressZoneId);
        if (zones != Match.Hit)
        {
            return zones == Match.Miss ? Match.Miss : Match.Indeterminate;
        }

        if (packet.Protocol is null)
        {
            if (!cube.Protocols.IsUniverse)
            {
                return Match.Indeterminate;
            }
        }
        else if (!cube.Protocols.Contains(packet.Protocol.Value))
        {
            return Match.Miss;
        }

        if (packet.Protocol is byte proto
            && (proto == IpProtocol.Tcp || proto == IpProtocol.Udp || proto == IpProtocol.Sctp))
        {
            if (packet.SourcePort is ushort src && !ContainsPort(cube.SourcePorts, src))
            {
                return Match.Miss;
            }

            if (packet.DestinationPort is ushort dst && !ContainsPort(cube.DestinationPorts, dst))
            {
                return Match.Miss;
            }

            if ((packet.SourcePort is null && !IsUniversePorts(cube.SourcePorts))
                || (packet.DestinationPort is null && !IsUniversePorts(cube.DestinationPorts)))
            {
                return Match.Indeterminate;
            }
        }

        Match states = ContainsOptional(cube.ConnectionStates, packet.ConnectionState);
        if (states != Match.Hit)
        {
            return states;
        }

        states = ContainsOptional(cube.ConnectionNatStates, packet.ConnectionNatState);
        if (states != Match.Hit)
        {
            return states;
        }

        states = ContainsOptional(cube.SourceAddressTypes, packet.SourceAddressType);
        if (states != Match.Hit)
        {
            return states;
        }

        states = ContainsOptional(cube.DestinationAddressTypes, packet.DestinationAddressType);
        if (states != Match.Hit)
        {
            return states;
        }

        if (cube.IcmpSelectors is { Items.Count: > 0 } icmp)
        {
            if (packet.IcmpType is null)
            {
                return Match.Indeterminate;
            }

            bool typeHit = false;
            bool needsCode = false;
            foreach (IcmpSelector selector in icmp.Items)
            {
                if (selector.Type != packet.IcmpType.Value)
                {
                    continue;
                }

                if (selector.Code is null)
                {
                    typeHit = true;
                    break;
                }

                if (packet.IcmpCode is null)
                {
                    needsCode = true;
                    continue;
                }

                if (selector.Code.Value == packet.IcmpCode.Value)
                {
                    typeHit = true;
                    break;
                }
            }

            if (!typeHit)
            {
                return needsCode ? Match.Indeterminate : Match.Miss;
            }
        }

        if (cube.TcpFlags is { RequiredPresent.Count: > 0 } flags)
        {
            if (packet.TcpFlagPresent is null)
            {
                return Match.Indeterminate;
            }

            if (!flags.RequiredPresent.Contains(packet.TcpFlagPresent.Value))
            {
                return Match.Miss;
            }
        }

        if (cube.IpsecPolicy is not null)
        {
            if (packet.IpsecDirection is null)
            {
                return Match.Indeterminate;
            }

            if (cube.IpsecPolicy.Direction != packet.IpsecDirection.Value)
            {
                return Match.Miss;
            }
        }

        return Match.Hit;
    }

    private static Match TryActualFilter(
        PolicyTestPacket packet,
        IReadOnlyList<ActualFilterRule> actual,
        bool preAnchor,
        List<PolicyTestPathHop> path,
        out PolicyTestExpectedDisposition? disposition)
    {
        disposition = null;
        string chain = FormatChain(packet.Chain);
        List<ActualFilterRule> surface = actual
            .Where(r => r.Family == packet.Family
                        && string.Equals(r.Chain, chain, StringComparison.OrdinalIgnoreCase)
                        && !r.Disabled
                        && !ActualFilterMarker.IsManagedChainName(r.Chain))
            .OrderBy(static r => r.Ordinal)
            .ToList();
        int? anchor = surface.FirstOrDefault(static r => ActualFilterMarker.IsAnchor(r.Comment))?.Ordinal;
        foreach (ActualFilterRule rule in surface)
        {
            bool isPre = anchor is null || rule.Ordinal < anchor.Value;
            bool isPost = anchor is not null && rule.Ordinal > anchor.Value;
            if (preAnchor && !isPre)
            {
                continue;
            }

            if (!preAnchor && !isPost)
            {
                continue;
            }

            if (ActualFilterMarker.IsAnchor(rule.Comment) || !ActualFilterMarker.IsUnmanaged(rule.Comment))
            {
                continue;
            }

            if (rule.UnknownMatchers.Count > 0)
            {
                path.Add(Hop(PolicyTestPathKind.UnmanagedRule, $"{chain}#{rule.Ordinal}", null, null));
                return Match.Indeterminate;
            }

            Match match = MatchActual(rule, packet);
            if (match == Match.Indeterminate)
            {
                path.Add(Hop(PolicyTestPathKind.UnmanagedRule, $"{chain}#{rule.Ordinal}", null, null));
                return Match.Indeterminate;
            }

            if (match == Match.Miss)
            {
                continue;
            }

            PolicyTestExpectedDisposition? mapped = MapActualAction(rule.Action);
            if (mapped is null)
            {
                path.Add(Hop(PolicyTestPathKind.UnmanagedRule, $"{chain}#{rule.Ordinal}", null, null));
                return Match.Indeterminate;
            }

            PolicyTestPathKind kind = preAnchor ? PolicyTestPathKind.UnmanagedRule : PolicyTestPathKind.PostAnchorRule;
            path.Add(Hop(kind, $"{chain}#{rule.Ordinal}", null, null));
            disposition = mapped;
            return Match.Hit;
        }

        return Match.Miss;
    }

    private static Match MatchActual(ActualFilterRule rule, PolicyTestPacket packet)
    {
        foreach (string key in rule.KnownMatchers.Keys)
        {
            if (!ProvenActualMatchers.Contains(key))
            {
                return Match.Indeterminate;
            }
        }

        if (rule.KnownMatchers.TryGetValue("protocol", out string? protocol))
        {
            if (packet.Protocol is null)
            {
                return Match.Indeterminate;
            }

            if (!string.Equals(protocol, packet.Protocol.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                && !string.Equals(protocol, ProtocolName(packet.Protocol.Value), StringComparison.OrdinalIgnoreCase))
            {
                if (byte.TryParse(protocol, NumberStyles.None, CultureInfo.InvariantCulture, out _)
                    || IsNamedProtocol(protocol))
                {
                    return Match.Miss;
                }

                return Match.Indeterminate;
            }
        }

        if (rule.KnownMatchers.TryGetValue("src-address", out string? src))
        {
            Match address = MatchExactHost(src, packet.SourceAddress);
            if (address != Match.Hit)
            {
                return address;
            }
        }

        if (rule.KnownMatchers.TryGetValue("dst-address", out string? dst))
        {
            Match address = MatchExactHost(dst, packet.DestinationAddress);
            if (address != Match.Hit)
            {
                return address;
            }
        }

        if (rule.KnownMatchers.TryGetValue("connection-state", out string? states))
        {
            if (packet.ConnectionState is null)
            {
                return Match.Indeterminate;
            }

            string expected = packet.ConnectionState.Value.ToString();
            string[] parts = states.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!parts.Any(p => string.Equals(p, expected, StringComparison.OrdinalIgnoreCase)))
            {
                return Match.Miss;
            }
        }

        return Match.Hit;
    }

    private static Match MatchExactHost(string matcher, string packetAddress)
    {
        if (string.IsNullOrWhiteSpace(packetAddress)
            || matcher.Contains('/', StringComparison.Ordinal)
            || matcher.Contains('-', StringComparison.Ordinal)
            || matcher.Contains(',', StringComparison.Ordinal)
            || !IPAddress.TryParse(matcher, out _))
        {
            return Match.Indeterminate;
        }

        return string.Equals(matcher, packetAddress, StringComparison.OrdinalIgnoreCase)
            ? Match.Hit
            : Match.Miss;
    }

    private static bool ContainsAddress(IReadOnlyList<AddressInterval> intervals, IpAddressFamily family, string text)
    {
        if (!IPAddress.TryParse(text, out IPAddress? parsed))
        {
            return false;
        }

        UInt128 value;
        try
        {
            value = AddressInterval.ToNumeric(parsed, family);
        }
        catch (DomainInvariantException)
        {
            return false;
        }

        return intervals.Any(i => i.Family == family && value >= i.Start && value <= i.End);
    }

    private static bool ContainsPort(IReadOnlyList<PortInterval> intervals, ushort port)
        => intervals.Any(i => port >= i.Start && port <= i.End);

    private static bool IsUniversePorts(IReadOnlyList<PortInterval> intervals)
        => intervals.Count == 1 && intervals[0].Start == 0 && intervals[0].End == ushort.MaxValue;

    private static Match ContainsOptional<T>(SymbolicSet<T> set, T? value)
        where T : struct
    {
        if (set.IsUniverse && set.Members.Count == 0)
        {
            return Match.Hit;
        }

        if (value is null)
        {
            return set.IsEmpty ? Match.Miss : Match.Indeterminate;
        }

        T item = value.Value;
        if (set.IsUniverse)
        {
            return set.Members.Contains(item) ? Match.Miss : Match.Hit;
        }

        return set.Members.Contains(item) ? Match.Hit : Match.Miss;
    }

    private static PolicyTestExpectedDisposition MapEffect(PolicyRuleEffect effect)
        => effect switch
        {
            PolicyRuleEffect.Accept => PolicyTestExpectedDisposition.Accept,
            PolicyRuleEffect.Drop => PolicyTestExpectedDisposition.Drop,
            PolicyRuleEffect.Reject => PolicyTestExpectedDisposition.Reject,
            PolicyRuleEffect.FasttrackAccept => PolicyTestExpectedDisposition.FasttrackAccept,
            _ => PolicyTestExpectedDisposition.Drop,
        };

    private static PolicyTestExpectedDisposition MapDefault(ChainDefaultDisposition disposition)
        => disposition switch
        {
            ChainDefaultDisposition.Drop => PolicyTestExpectedDisposition.Drop,
            ChainDefaultDisposition.Reject => PolicyTestExpectedDisposition.Reject,
            ChainDefaultDisposition.ReturnToUnmanaged => PolicyTestExpectedDisposition.ReturnToUnmanaged,
            _ => PolicyTestExpectedDisposition.Drop,
        };

    private static PolicyTestExpectedDisposition? MapActualAction(string? action)
        => action switch
        {
            "accept" => PolicyTestExpectedDisposition.Accept,
            "drop" => PolicyTestExpectedDisposition.Drop,
            "reject" => PolicyTestExpectedDisposition.Reject,
            "fasttrack-connection" => PolicyTestExpectedDisposition.FasttrackAccept,
            "return" => PolicyTestExpectedDisposition.ReturnToUnmanaged,
            _ => null,
        };

    private static string FormatChain(PolicyFilterChain chain)
        => chain switch
        {
            PolicyFilterChain.Input => "input",
            PolicyFilterChain.Forward => "forward",
            PolicyFilterChain.Output => "output",
            _ => "forward",
        };

    private static string ProtocolName(byte number)
        => number switch
        {
            IpProtocol.Tcp => "tcp",
            IpProtocol.Udp => "udp",
            IpProtocol.Icmp => "icmp",
            IpProtocol.IcmpV6 => "icmpv6",
            IpProtocol.Sctp => "sctp",
            IpProtocol.Vrrp => "vrrp",
            _ => number.ToString(CultureInfo.InvariantCulture),
        };

    private static bool IsNamedProtocol(string protocol)
        => protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase)
           || protocol.Equals("udp", StringComparison.OrdinalIgnoreCase)
           || protocol.Equals("icmp", StringComparison.OrdinalIgnoreCase)
           || protocol.Equals("icmpv6", StringComparison.OrdinalIgnoreCase)
           || protocol.Equals("sctp", StringComparison.OrdinalIgnoreCase)
           || protocol.Equals("vrrp", StringComparison.OrdinalIgnoreCase);

    private static PolicyTestPathHop Hop(
        PolicyTestPathKind kind,
        string? subject,
        PolicyPipelineStage? stage,
        RuleId? ruleId)
        => new()
        {
            Kind = kind,
            Subject = subject,
            Stage = stage,
            RuleId = ruleId,
        };

    private static PolicyTestResult Result(
        PolicyTestCase test,
        string outcome,
        IReadOnlyList<PolicyTestPathHop> path,
        PolicyTestExpectedDisposition actual,
        string proof,
        string? failureCode,
        RuleId? matchedRuleId = null,
        PolicyPipelineStage? matchedStage = null)
        => new()
        {
            TestId = test.Id,
            Outcome = outcome,
            MatchedPath = path,
            MatchedRuleId = matchedRuleId,
            MatchedStage = matchedStage,
            FinalDisposition = actual,
            Proof = proof,
            FailureCode = outcome == PolicyEvidenceAnalysisCodes.OutcomeFail ? failureCode : null,
        };
}
