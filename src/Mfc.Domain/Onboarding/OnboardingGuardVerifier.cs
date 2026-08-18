using System.Globalization;
using System.Net;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Verifies an external management guard against a typed <see cref="GuardProfile"/>
/// (Onboarding Spec §13–§17 / Issue Set M5-03). Does not create, move, or rewrite guards.
/// </summary>
public static class OnboardingGuardVerifier
{
    public const string AnalyzerVersion = "mfc.onboarding.guard.v1";

    private static readonly HashSet<string> InputAllow = new(StringComparer.Ordinal)
    {
        "protocol",
        "src-address",
        "dst-address",
        "dst-port",
        "connection-state",
        "in-interface",
        "in-interface-list",
    };

    private static readonly HashSet<string> OutputAllow = new(StringComparer.Ordinal)
    {
        "protocol",
        "src-address",
        "dst-address",
        "src-port",
        "connection-state",
        "out-interface",
        "out-interface-list",
    };

    private static readonly HashSet<string> ForbiddenMatchers = new(StringComparer.Ordinal)
    {
        "src-address-list",
        "dst-address-list",
        "address-list",
        "layer7-protocol",
        "content",
        "tls-host",
        "packet-mark",
        "connection-mark",
        "routing-mark",
        "random",
        "nth",
        "pcc",
        "time",
        "dscp",
        "hotspot",
        "ipv4-options",
        "ttl",
    };

    /// <summary>
    /// Validates live filter rules against <paramref name="profile"/> and the plan's expected guard hash.
    /// Optional placements enforce "guard before planned anchors" (AC#6).
    /// </summary>
    public static OnboardingGuardVerificationResult Verify(
        GuardProfile profile,
        IReadOnlyList<ActualFilterRule> rules,
        Hash256 expectedGuardHash,
        IReadOnlyList<AnchorPlacement>? plannedPlacements = null,
        IReadOnlyList<string>? candidateComments = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(expectedGuardHash);

        List<OnboardingGuardFinding> findings = [];
        if (!profile.CanonicalHash.Equals(expectedGuardHash))
        {
            findings.Add(Blocker(
                OnboardingCodes.ManagementGuardInvalid,
                "GuardProfile canonical hash does not match DeviceOnboardingPlan.expected_guard_hash.",
                profile.DeviceId,
                target: "guard.hash"));
        }

        foreach (string comment in candidateComments ?? [])
        {
            if (ActualFilterMarker.IsGuard(comment))
            {
                findings.Add(Blocker(
                    ManagementPathAnalysisCodes.GuardMoved,
                    "Candidate policy contains a management-guard marker; Controller must not create or modify guards.",
                    profile.DeviceId,
                    target: "candidate"));
                break;
            }
        }

        Dictionary<string, List<ActualFilterRule>> byMarker = IndexRulesByMarker(rules, profile, findings);
        VerifyMarkerSet(profile, profile.InputRuleMarkers, FilterBuiltInContext.Input, byMarker, findings);
        VerifyMarkerSet(profile, profile.OutputRuleMarkers, FilterBuiltInContext.Output, byMarker, findings);
        RejectUnlistedProfileGuards(profile, rules, findings);

        foreach (string marker in profile.InputRuleMarkers)
        {
            if (byMarker.TryGetValue(marker, out List<ActualFilterRule>? matched))
            {
                foreach (ActualFilterRule rule in matched.Where(static r => !r.Disabled))
                {
                    EvaluateRule(profile, rule, requireNew: true, plannedPlacements, findings);
                }
            }
        }

        foreach (string marker in profile.OutputRuleMarkers)
        {
            if (byMarker.TryGetValue(marker, out List<ActualFilterRule>? matched))
            {
                foreach (ActualFilterRule rule in matched.Where(static r => !r.Disabled))
                {
                    EvaluateRule(profile, rule, requireNew: false, plannedPlacements, findings);
                }
            }
        }

        CheckLiveAnchors(profile, rules, findings);

        IReadOnlyList<OnboardingGuardFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.Chain, f.Ordinal, f.Message, f.Target))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Chain ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static f => f.Ordinal ?? -1)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();

        return new OnboardingGuardVerificationResult
        {
            Findings = ordered,
            GuardHash = profile.CanonicalHash,
        };
    }

    private static Dictionary<string, List<ActualFilterRule>> IndexRulesByMarker(
        IReadOnlyList<ActualFilterRule> rules,
        GuardProfile profile,
        List<OnboardingGuardFinding> findings)
    {
        Dictionary<string, List<ActualFilterRule>> map = new(StringComparer.Ordinal);
        HashSet<string> seenMarkers = new(StringComparer.Ordinal);
        foreach (ActualFilterRule rule in rules)
        {
            if (!ActualFilterMarker.IsGuard(rule.Comment))
            {
                continue;
            }

            if (!GuardMarker.TryParse(
                    rule.Comment,
                    out GuardProfileId id,
                    out IpAddressFamily family,
                    out FilterBuiltInContext chain,
                    out _))
            {
                if (rule.Family == profile.Family)
                {
                    findings.Add(Blocker(
                        OnboardingCodes.ManagementGuardInvalid,
                        "Guard comment is not a strict mfc:guard:v1 marker at the first character.",
                        profile.DeviceId,
                        rule.Chain,
                        rule.Ordinal,
                        "marker"));
                }

                continue;
            }

            if (!ActualFilterMarker.TryReadMarker(rule.Comment, out string? marker) || marker is null)
            {
                continue;
            }

            if (!seenMarkers.Add(marker))
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardInvalid,
                    $"Guard marker '{marker}' is not unique on the Device.",
                    profile.DeviceId,
                    ChainName(chain),
                    rule.Ordinal,
                    "marker"));
            }

            if (id.Value != profile.Id.Value || family != profile.Family)
            {
                continue;
            }

            if (!map.TryGetValue(marker, out List<ActualFilterRule>? list))
            {
                list = [];
                map[marker] = list;
            }

            list.Add(rule);
        }

        return map;
    }

    private static void VerifyMarkerSet(
        GuardProfile profile,
        IReadOnlyList<string> expectedMarkers,
        FilterBuiltInContext expectedChain,
        Dictionary<string, List<ActualFilterRule>> byMarker,
        List<OnboardingGuardFinding> findings)
    {
        string chainName = ChainName(expectedChain);
        foreach (string marker in expectedMarkers)
        {
            if (!byMarker.TryGetValue(marker, out List<ActualFilterRule>? matches) || matches.Count == 0)
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardMissing,
                    $"Expected {chainName} guard marker '{marker}' was not found.",
                    profile.DeviceId,
                    chainName,
                    target: "marker"));
                continue;
            }

            ActualFilterRule? enabled = matches.FirstOrDefault(static r => !r.Disabled);
            if (enabled is null)
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardInvalid,
                    $"Guard marker '{marker}' exists but is disabled.",
                    profile.DeviceId,
                    chainName,
                    matches[0].Ordinal,
                    "enabled"));
                continue;
            }

            if (matches.Count(static r => !r.Disabled) > 1)
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardInvalid,
                    $"Guard marker '{marker}' matches multiple enabled rules.",
                    profile.DeviceId,
                    chainName,
                    enabled.Ordinal,
                    "marker"));
            }

            string expectedChainName = ChainName(expectedChain);
            if (!string.Equals(enabled.Chain, expectedChainName, StringComparison.OrdinalIgnoreCase)
                || enabled.Family != profile.Family)
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardInvalid,
                    $"Guard marker '{marker}' is on unexpected chain/family.",
                    profile.DeviceId,
                    enabled.Chain,
                    enabled.Ordinal,
                    "marker"));
            }
        }
    }

    private static void RejectUnlistedProfileGuards(
        GuardProfile profile,
        IReadOnlyList<ActualFilterRule> rules,
        List<OnboardingGuardFinding> findings)
    {
        HashSet<string> listed = new(profile.InputRuleMarkers.Concat(profile.OutputRuleMarkers), StringComparer.Ordinal);
        foreach (ActualFilterRule rule in rules)
        {
            if (rule.Disabled
                || rule.Family != profile.Family
                || !GuardMarker.TryParse(rule.Comment, out GuardProfileId id, out _, out _, out _)
                || id.Value != profile.Id.Value
                || !ActualFilterMarker.TryReadMarker(rule.Comment, out string? marker)
                || marker is null
                || listed.Contains(marker))
            {
                continue;
            }

            findings.Add(Blocker(
                OnboardingCodes.ManagementGuardInvalid,
                $"Live guard marker '{marker}' is not listed on GuardProfile.",
                profile.DeviceId,
                rule.Chain,
                rule.Ordinal,
                "marker"));
        }
    }

    private static void EvaluateRule(
        GuardProfile profile,
        ActualFilterRule rule,
        bool requireNew,
        IReadOnlyList<AnchorPlacement>? plannedPlacements,
        List<OnboardingGuardFinding> findings)
    {
        string chain = rule.Chain;
        if (rule.Dynamic)
        {
            findings.Add(Blocker(
                OnboardingCodes.ManagementGuardInvalid,
                "Management guard must be static (dynamic rules are rejected).",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "static"));
            return;
        }

        CheckPlacement(profile, rule, plannedPlacements, findings);

        if (rule.UnknownMatchers.Count > 0)
        {
            findings.Add(Blocker(
                OnboardingCodes.ManagementPathIndeterminate,
                "Management guard has an unknown matcher.",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "matcher"));
            return;
        }

        HashSet<string> allow = requireNew ? InputAllow : OutputAllow;
        foreach (string key in rule.KnownMatchers.Keys)
        {
            if (ForbiddenMatchers.Contains(key)
                || key.Contains("address-list", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardInvalid,
                    $"Management guard uses forbidden matcher '{key}' (dynamic list / Spec §16.3).",
                    profile.DeviceId,
                    chain,
                    rule.Ordinal,
                    "matcher"));
                return;
            }

            if (!allow.Contains(key))
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardInvalid,
                    $"Management guard uses unsupported matcher '{key}'.",
                    profile.DeviceId,
                    chain,
                    rule.Ordinal,
                    "matcher"));
                return;
            }
        }

        if (!string.Equals(rule.Action, "accept", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Blocker(
                requireNew
                    ? ManagementPathAnalysisCodes.InputBlocked
                    : ManagementPathAnalysisCodes.OutputBlocked,
                $"Management guard action '{rule.Action ?? "(missing)"}' is not accept.",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "action"));
            return;
        }

        if (!IsTcp(Known(rule, "protocol")))
        {
            findings.Add(Blocker(
                requireNew
                    ? ManagementPathAnalysisCodes.InputBlocked
                    : ManagementPathAnalysisCodes.OutputBlocked,
                "Management guard protocol is not tcp.",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "protocol"));
            return;
        }

        string? states = Known(rule, "connection-state");
        if (string.IsNullOrWhiteSpace(states))
        {
            findings.Add(Blocker(
                OnboardingCodes.ManagementPathIndeterminate,
                "Management guard omits connection-state.",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "connection-state"));
            return;
        }

        HashSet<string> allowedStates = requireNew
            ? new(StringComparer.OrdinalIgnoreCase) { "new", "established" }
            : new(StringComparer.OrdinalIgnoreCase) { "established", "related" };
        string[] stateTokens = states.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (stateTokens.Length == 0
            || stateTokens.Any(t => !allowedStates.Contains(t)))
        {
            findings.Add(Blocker(
                OnboardingCodes.ManagementGuardTooBroad,
                $"Management guard connection-state '{states}' is wider than Spec §16.",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "connection-state"));
            return;
        }

        if (requireNew && !HasToken(states, "new"))
        {
            findings.Add(Blocker(
                ManagementPathAnalysisCodes.InputBlocked,
                "Management input guard does not allow TCP NEW (API-SSL connection).",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "connection-state"));
            return;
        }

        if (!requireNew && !HasToken(states, "established"))
        {
            findings.Add(Blocker(
                ManagementPathAnalysisCodes.OutputBlocked,
                "Management output guard does not allow TCP ESTABLISHED.",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "connection-state"));
            return;
        }

        string? portField = requireNew ? Known(rule, "dst-port") : Known(rule, "src-port");
        if (!IsExactPort(portField, profile.ApiSslPort))
        {
            findings.Add(Blocker(
                OnboardingCodes.ManagementGuardTooBroad,
                $"Management guard port '{portField ?? "(missing)"}' must equal API-SSL {profile.ApiSslPort} exactly.",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "port"));
            return;
        }

        string? controllerField = requireNew ? Known(rule, "src-address") : Known(rule, "dst-address");
        if (!TryParsePrefixes(controllerField, out List<AddressPrefix> controllerMatchers, out string? parseError))
        {
            findings.Add(Blocker(
                OnboardingCodes.ManagementPathIndeterminate,
                parseError ?? "Controller source matcher cannot be parsed.",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "src"));
            return;
        }

        foreach (AddressPrefix matcher in controllerMatchers)
        {
            if (GuardProfile.IsDefaultRoute(matcher))
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardTooBroad,
                    $"Management guard rejects default route '{matcher}'.",
                    profile.DeviceId,
                    chain,
                    rule.Ordinal,
                    "breadth"));
                return;
            }

            if (!profile.ControllerSourcePrefixes.Any(p => p.Contains(matcher)))
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardTooBroad,
                    $"Guard matcher '{matcher}' is wider than GuardProfile controller_source_prefixes.",
                    profile.DeviceId,
                    chain,
                    rule.Ordinal,
                    "breadth"));
                return;
            }
        }

        foreach (AddressPrefix required in profile.ControllerSourcePrefixes)
        {
            if (!controllerMatchers.Any(m => m.Contains(required)))
            {
                findings.Add(Blocker(
                    requireNew
                        ? ManagementPathAnalysisCodes.InputBlocked
                        : ManagementPathAnalysisCodes.OutputBlocked,
                    $"Guard matcher '{controllerField}' does not cover controller prefix {required}.",
                    profile.DeviceId,
                    chain,
                    rule.Ordinal,
                    "coverage"));
                return;
            }
        }

        string? deviceField = requireNew ? Known(rule, "dst-address") : Known(rule, "src-address");
        if (!TryParsePrefixes(deviceField, out List<AddressPrefix> deviceMatchers, out string? deviceError))
        {
            findings.Add(Blocker(
                OnboardingCodes.ManagementPathIndeterminate,
                deviceError ?? "Management address matcher cannot be parsed.",
                profile.DeviceId,
                chain,
                rule.Ordinal,
                "dst"));
            return;
        }

        byte hostBits = profile.Family == IpAddressFamily.IPv4 ? (byte)32 : (byte)128;
        AddressPrefix hostPrefix = AddressPrefix.Create(profile.ManagementDestination, hostBits);
        foreach (AddressPrefix matcher in deviceMatchers)
        {
            if (GuardProfile.IsDefaultRoute(matcher))
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardTooBroad,
                    $"Management guard rejects default route '{matcher}' on the device address field.",
                    profile.DeviceId,
                    chain,
                    rule.Ordinal,
                    "breadth"));
                return;
            }

            // Spec §16: destination/source = physical management address (not a supernet).
            if (!hostPrefix.Contains(matcher) || !matcher.Contains(profile.ManagementDestination))
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardTooBroad,
                    $"Guard device address '{matcher}' is wider than physical management destination {profile.ManagementDestination}.",
                    profile.DeviceId,
                    chain,
                    rule.Ordinal,
                    "breadth"));
                return;
            }
        }

        if (requireNew && profile.IngressInterfaceSet.Count > 0)
        {
            string? iface = Known(rule, "in-interface") ?? Known(rule, "in-interface-list");
            if (iface is null || !profile.IngressInterfaceSet.Contains(iface, StringComparer.Ordinal))
            {
                findings.Add(Blocker(
                    OnboardingCodes.ManagementGuardInvalid,
                    $"Input guard interface '{iface ?? "(missing)"}' is outside ingress_interface_set.",
                    profile.DeviceId,
                    chain,
                    rule.Ordinal,
                    "interface"));
            }
        }
    }

    private static void CheckPlacement(
        GuardProfile profile,
        ActualFilterRule rule,
        IReadOnlyList<AnchorPlacement>? plannedPlacements,
        List<OnboardingGuardFinding> findings)
    {
        FilterBuiltInContext? chainCtx = ParseChain(rule.Chain);
        if (chainCtx is null)
        {
            return;
        }

        if (plannedPlacements is null)
        {
            return;
        }

        foreach (AnchorPlacement placement in plannedPlacements)
        {
            if (placement.Family != profile.Family || placement.Chain != chainCtx.Value)
            {
                continue;
            }

            if (rule.Ordinal >= placement.ExpectedAnchorOrdinal)
            {
                findings.Add(Blocker(
                    ManagementPathAnalysisCodes.GuardMoved,
                    $"Guard at ordinal {rule.Ordinal} is not before planned anchor ordinal {placement.ExpectedAnchorOrdinal}.",
                    profile.DeviceId,
                    rule.Chain,
                    rule.Ordinal,
                    "placement"));
            }
        }
    }

    /// <summary>
    /// Ensures each enabled profile guard precedes any live anchor on the same chain/family.
    /// </summary>
    private static void CheckLiveAnchors(
        GuardProfile profile,
        IReadOnlyList<ActualFilterRule> rules,
        List<OnboardingGuardFinding> findings)
    {
        foreach (string chainName in new[] { "input", "output" })
        {
            List<ActualFilterRule> chainRules = rules
                .Where(r => r.Family == profile.Family
                            && !r.Disabled
                            && string.Equals(r.Chain, chainName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(static r => r.Ordinal)
                .ToList();
            ActualFilterRule? anchor = chainRules.FirstOrDefault(static r => ActualFilterMarker.IsAnchor(r.Comment));
            if (anchor is null)
            {
                continue;
            }

            foreach (ActualFilterRule guard in chainRules.Where(static r => ActualFilterMarker.IsGuard(r.Comment)))
            {
                if (!GuardMarker.TryParse(guard.Comment, out GuardProfileId id, out _, out _, out _)
                    || id.Value != profile.Id.Value)
                {
                    continue;
                }

                if (guard.Ordinal >= anchor.Ordinal)
                {
                    findings.Add(Blocker(
                        ManagementPathAnalysisCodes.GuardMoved,
                        $"Guard at ordinal {guard.Ordinal} is not before live anchor at {anchor.Ordinal}.",
                        profile.DeviceId,
                        chainName,
                        guard.Ordinal,
                        "placement"));
                }
            }
        }
    }

    private static bool TryParsePrefixes(string? csv, out List<AddressPrefix> prefixes, out string? error)
    {
        prefixes = [];
        error = null;
        if (string.IsNullOrWhiteSpace(csv))
        {
            error = "Address matcher is missing.";
            return false;
        }

        foreach (string token in csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParsePrefixOrHost(token, out AddressPrefix? prefix) && prefix is not null)
            {
                prefixes.Add(prefix);
                continue;
            }

            error = $"Address matcher '{token}' cannot be parsed.";
            prefixes = [];
            return false;
        }

        if (prefixes.Count == 0)
        {
            error = "Address matcher list is empty.";
            return false;
        }

        return true;
    }

    private static bool TryParsePrefixOrHost(string token, out AddressPrefix? prefix)
    {
        prefix = null;
        try
        {
            if (token.Contains('/', StringComparison.Ordinal))
            {
                prefix = AddressPrefix.Parse(token);
                return true;
            }

            if (!IPAddress.TryParse(token, out IPAddress? address))
            {
                return false;
            }

            byte bits = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                ? (byte)32
                : (byte)128;
            prefix = AddressPrefix.Create(address, bits);
            return true;
        }
        catch (DomainInvariantException)
        {
            return false;
        }
    }

    private static string? Known(ActualFilterRule rule, string key)
        => rule.KnownMatchers.TryGetValue(key, out string? value) ? value : null;

    private static bool IsTcp(string? protocol)
        => string.Equals(protocol, "tcp", StringComparison.OrdinalIgnoreCase)
           || string.Equals(protocol, "6", StringComparison.Ordinal);

    private static bool HasToken(string csv, string token)
        => csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(t => string.Equals(t, token, StringComparison.OrdinalIgnoreCase));

    private static bool IsExactPort(string? field, ushort port)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return false;
        }

        string target = port.ToString(CultureInfo.InvariantCulture);
        string[] tokens = field.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length == 1 && string.Equals(tokens[0], target, StringComparison.Ordinal);
    }

    private static FilterBuiltInContext? ParseChain(string chain)
        => chain.ToLowerInvariant() switch
        {
            "input" => FilterBuiltInContext.Input,
            "output" => FilterBuiltInContext.Output,
            "forward" => FilterBuiltInContext.Forward,
            _ => null,
        };

    private static string ChainName(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Output => "output",
            FilterBuiltInContext.Forward => "forward",
            _ => chain.ToString().ToLowerInvariant(),
        };

    private static OnboardingGuardFinding Blocker(
        string code,
        string message,
        DeviceId? deviceId,
        string? chain = null,
        int? ordinal = null,
        string? target = null)
        => new()
        {
            Code = code,
            Severity = OnboardingCodes.SeverityBlocker,
            Message = message,
            DeviceId = deviceId,
            Chain = chain,
            Ordinal = ordinal,
            Target = target,
        };
}
