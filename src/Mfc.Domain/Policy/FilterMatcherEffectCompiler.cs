using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Normative physical-rule limits (Compiler Spec §27, layout v1).</summary>
public sealed class FilterRuleCompileLimits
{
    public const int LayoutV1MaxPhysicalRulesPerFamilyChain = 20_000;

    public static FilterRuleCompileLimits LayoutV1 { get; } = new()
    {
        MaxPhysicalRulesPerFamilyChain = LayoutV1MaxPhysicalRulesPerFamilyChain,
    };

    public required int MaxPhysicalRulesPerFamilyChain { get; init; }

    public void EnsureWithinLayoutV1()
    {
        if (MaxPhysicalRulesPerFamilyChain is < 1 or > LayoutV1MaxPhysicalRulesPerFamilyChain)
        {
            throw new DomainInvariantException(
                $"MaxPhysicalRulesPerFamilyChain must be between 1 and {LayoutV1MaxPhysicalRulesPerFamilyChain} (layout v1).");
        }
    }
}

/// <summary>Per-device catalogs for matcher compilation. Active WAN is ignored by zone expansion.</summary>
public sealed class FilterMatcherCompileContext
{
    public required ZoneServiceCompileContext Zones { get; init; }

    public required IReadOnlyDictionary<AddressObjectId, AddressObject> Addresses { get; init; }
}

/// <summary>Outcome of compiling logical rules into physical filter artifacts (no partial payload on failure).</summary>
public sealed class FilterRuleCompileResult
{
    private FilterRuleCompileResult(
        bool isSuccess,
        string? code,
        string? message,
        IReadOnlyList<FilterRuleArtifact> rules,
        IReadOnlyList<AddressListArtifactDraft> lists)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
        Rules = rules;
        InternedLists = lists;
    }

    public bool IsSuccess { get; }

    public string? Code { get; }

    public string? Message { get; }

    public IReadOnlyList<FilterRuleArtifact> Rules { get; }

    public IReadOnlyList<AddressListArtifactDraft> InternedLists { get; }

    public static FilterRuleCompileResult Ok(
        IReadOnlyList<FilterRuleArtifact> rules,
        IReadOnlyList<AddressListArtifactDraft> lists)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(lists);
        return new FilterRuleCompileResult(true, null, null, rules, lists);
    }

    public static FilterRuleCompileResult Fail(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new FilterRuleCompileResult(false, code, message, [], []);
    }
}

/// <summary>
/// Compiles supported matchers and regular effects (M3-05, Compiler Spec §15 / §20 / §23).
/// Pure Domain: preserves input-list order, does not delete duplicate logical rules,
/// does not emit FastTrack pairs (M3-06), and never writes RouterOS.
/// </summary>
public sealed class FilterMatcherEffectCompiler
{
    private readonly AddressListCompileLimits _addressListLimits;

    private readonly ZoneServiceCompileLimits _zoneServiceLimits;

    public FilterMatcherEffectCompiler(
        FilterRuleCompileLimits? limits = null,
        AddressListCompileLimits? addressListLimits = null,
        ZoneServiceCompileLimits? zoneServiceLimits = null)
    {
        Limits = limits ?? FilterRuleCompileLimits.LayoutV1;
        Limits.EnsureWithinLayoutV1();
        _addressListLimits = addressListLimits ?? AddressListCompileLimits.LayoutV1;
        _zoneServiceLimits = zoneServiceLimits ?? ZoneServiceCompileLimits.LayoutV1;
    }

    public FilterRuleCompileLimits Limits { get; }

    /// <summary>
    /// Compiles <paramref name="rules"/> in input-list order. Disabled rules are omitted;
    /// identical enabled rules both emit. Fail-closed: no rules and no interned lists on error.
    /// </summary>
    public FilterRuleCompileResult Compile(
        IReadOnlyList<PolicyRule> rules,
        FilterMatcherCompileContext context)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Zones);
        ArgumentNullException.ThrowIfNull(context.Addresses);

        AddressListCompileSession lists = new(_addressListLimits);
        ZoneServiceVariantCompiler zones = new(_zoneServiceLimits);
        List<FilterRuleArtifact> emitted = [];
        Dictionary<(IpAddressFamily Family, PolicyFilterChain Chain), int> counts = [];

        foreach (PolicyRule rule in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            if (!rule.Enabled)
            {
                continue;
            }

            FilterRuleCompileResult? mapped = TryMapEffect(
                rule,
                out string action,
                out IReadOnlyDictionary<string, string>? actionParameters);
            if (mapped is not null)
            {
                return mapped;
            }

            AddressListCompileResult address = lists.Compile(
                rule.Family,
                rule.Predicate.SourceAddresses,
                rule.Predicate.DestinationAddresses,
                context.Addresses);
            if (!address.IsSuccess)
            {
                return FilterRuleCompileResult.Fail(address.Code!, address.Message!);
            }

            ZoneServiceCompileResult variants = zones.Compile(
                rule.Family,
                rule.Predicate.IngressZones,
                rule.Predicate.EgressZones,
                rule.Predicate.Services,
                context.Zones);
            if (!variants.IsSuccess)
            {
                return FilterRuleCompileResult.Fail(variants.Code!, variants.Message!);
            }

            FilterRuleCompileResult? extras = TryCompileExtraMatchers(rule.Predicate, out List<CompiledMatcher> extraMatchers);
            if (extras is not null)
            {
                return extras;
            }

            (IpAddressFamily Family, PolicyFilterChain Chain) surface = (rule.Family, rule.Chain);
            int surfaceCount = counts.GetValueOrDefault(surface);
            if (surfaceCount + variants.Variants.Count > Limits.MaxPhysicalRulesPerFamilyChain)
            {
                return FilterRuleCompileResult.Fail(
                    PolicyCompilerCodes.FilterRuleLimit,
                    $"Physical filter rules for {PolicyPipelineV1.FormatFamily(rule.Family)} {PolicyPipelineV1.FormatFilterChain(rule.Chain)} would exceed {Limits.MaxPhysicalRulesPerFamilyChain}.");
            }

            foreach (CompiledPhysicalVariant variant in variants.Variants)
            {
                FilterRuleCompileResult? built = TryBuildRule(
                    rule,
                    variant,
                    address,
                    extraMatchers,
                    action,
                    actionParameters,
                    (uint)emitted.Count,
                    out FilterRuleArtifact artifact);
                if (built is not null)
                {
                    return built;
                }

                emitted.Add(artifact);
            }

            counts[surface] = surfaceCount + variants.Variants.Count;
        }

        return FilterRuleCompileResult.Ok(emitted, lists.InternedLists);
    }

    private static FilterRuleCompileResult? TryMapEffect(
        PolicyRule rule,
        out string action,
        out IReadOnlyDictionary<string, string>? actionParameters)
    {
        actionParameters = null;
        switch (rule.Effect.Kind)
        {
            case PolicyRuleEffect.Accept:
                action = "accept";
                return null;

            case PolicyRuleEffect.Drop:
                action = "drop";
                return null;

            case PolicyRuleEffect.Reject:
                RejectMode mode = rule.Effect.RejectModeValue
                    ?? throw new DomainInvariantException("REJECT requires reject_mode.");
                if (!RouterOsCompilerProfile.TryFormatRejectWith(mode, out string rejectWith, out string? code))
                {
                    action = string.Empty;
                    return FilterRuleCompileResult.Fail(
                        code ?? PolicyCompilerCodes.RejectModeUnsupported,
                        $"Reject mode '{mode}' has no compiler-profile mapping.");
                }

                action = "reject";
                actionParameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reject-with"] = rejectWith,
                };
                return null;

            case PolicyRuleEffect.ExemptDenyStage:
                action = "return";
                return null;

            case PolicyRuleEffect.FasttrackAccept:
                action = string.Empty;
                return FilterRuleCompileResult.Fail(
                    PolicyCompilerCodes.FasttrackContextUnsupported,
                    "FASTTRACK_ACCEPT pair emission is not part of matcher/effect compile; it is M3-06.");

            default:
                action = string.Empty;
                return FilterRuleCompileResult.Fail(
                    PolicyCompilerCodes.UnsupportedMatcher,
                    $"Unsupported rule effect '{rule.Effect.Kind}'.");
        }
    }

    private static FilterRuleCompileResult? TryCompileExtraMatchers(
        TrafficPredicate predicate,
        out List<CompiledMatcher> extras)
    {
        extras = [];
        if (predicate.ConnectionStates is { Count: > 0 })
        {
            if (!RouterOsCompilerProfile.TryFormatConnectionStates(
                    predicate.ConnectionStates,
                    out string token,
                    out string? code)
                || token.Length == 0)
            {
                return FilterRuleCompileResult.Fail(
                    code ?? PolicyCompilerCodes.UnsupportedMatcher,
                    "connection-state has no compiler-profile mapping.");
            }

            extras.Add(new CompiledMatcher { Key = "connection-state", Value = token });
        }

        if (predicate.ConnectionNatStates is { Count: > 0 })
        {
            if (!RouterOsCompilerProfile.TryFormatConnectionNatStates(
                    predicate.ConnectionNatStates,
                    out string token,
                    out string? code)
                || token.Length == 0)
            {
                return FilterRuleCompileResult.Fail(
                    code ?? PolicyCompilerCodes.UnsupportedMatcher,
                    "connection-nat-state has no compiler-profile mapping.");
            }

            extras.Add(new CompiledMatcher { Key = "connection-nat-state", Value = token });
        }

        if (predicate.SourceAddressTypes is { Count: > 0 })
        {
            if (!RouterOsCompilerProfile.TryFormatAddressTypes(
                    predicate.SourceAddressTypes,
                    out string token,
                    out string? code)
                || token.Length == 0)
            {
                return FilterRuleCompileResult.Fail(
                    code ?? PolicyCompilerCodes.UnsupportedMatcher,
                    "src-address-type has no compiler-profile mapping.");
            }

            extras.Add(new CompiledMatcher { Key = "src-address-type", Value = token });
        }

        if (predicate.DestinationAddressTypes is { Count: > 0 })
        {
            if (!RouterOsCompilerProfile.TryFormatAddressTypes(
                    predicate.DestinationAddressTypes,
                    out string token,
                    out string? code)
                || token.Length == 0)
            {
                return FilterRuleCompileResult.Fail(
                    code ?? PolicyCompilerCodes.UnsupportedMatcher,
                    "dst-address-type has no compiler-profile mapping.");
            }

            extras.Add(new CompiledMatcher { Key = "dst-address-type", Value = token });
        }

        if (predicate.TcpFlags is not null)
        {
            if (!RouterOsCompilerProfile.TryFormatTcpFlags(predicate.TcpFlags, out string token, out string? code))
            {
                return FilterRuleCompileResult.Fail(
                    code ?? PolicyCompilerCodes.UnsupportedMatcher,
                    "tcp-flags has no compiler-profile mapping.");
            }

            if (token.Length > 0)
            {
                extras.Add(new CompiledMatcher { Key = "tcp-flags", Value = token });
            }
        }

        if (predicate.IpsecPolicy is not null)
        {
            if (!RouterOsCompilerProfile.TryFormatIpsecPolicy(
                    predicate.IpsecPolicy,
                    out string token,
                    out string? code)
                || token.Length == 0)
            {
                return FilterRuleCompileResult.Fail(
                    code ?? PolicyCompilerCodes.UnsupportedMatcher,
                    "ipsec-policy has no compiler-profile mapping.");
            }

            extras.Add(new CompiledMatcher { Key = "ipsec-policy", Value = token });
        }

        return null;
    }

    private static FilterRuleCompileResult? TryBuildRule(
        PolicyRule rule,
        CompiledPhysicalVariant variant,
        AddressListCompileResult address,
        IReadOnlyList<CompiledMatcher> extras,
        string action,
        IReadOnlyDictionary<string, string>? actionParameters,
        uint ordinal,
        out FilterRuleArtifact artifact)
    {
        artifact = null!;
        Dictionary<string, string> matchers = new(StringComparer.Ordinal);
        if (address.Source is { EmitsMatcher: true, MatcherKey: not null, MatcherValue: not null })
        {
            matchers[address.Source.MatcherKey] = address.Source.MatcherValue;
        }

        if (address.Destination is { EmitsMatcher: true, MatcherKey: not null, MatcherValue: not null })
        {
            matchers[address.Destination.MatcherKey] = address.Destination.MatcherValue;
        }

        foreach (CompiledMatcher matcher in variant.Matchers.Concat(extras))
        {
            if (matchers.TryGetValue(matcher.Key, out string? existing)
                && !string.Equals(existing, matcher.Value, StringComparison.Ordinal))
            {
                return FilterRuleCompileResult.Fail(
                    PolicyCompilerCodes.UnsupportedMatcher,
                    $"Conflicting values for matcher '{matcher.Key}'.");
            }

            matchers[matcher.Key] = matcher.Value;
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach ((string key, string value) in matchers)
        {
            if (!RouterOsCompilerProfile.TryNormalizeMatcher(
                    key,
                    value,
                    out string normalizedKey,
                    out string normalizedValue,
                    out string? code))
            {
                return FilterRuleCompileResult.Fail(
                    code ?? PolicyCompilerCodes.UnsupportedMatcher,
                    $"Unsupported matcher token '{key}={value}'.");
            }

            normalized[normalizedKey] = normalizedValue;
        }

        bool exception = rule.Effect.Kind == PolicyRuleEffect.ExemptDenyStage;
        string comment = exception
            ? CompilerComments.Exception(rule.Id.Value, variant.VariantIndex)
            : CompilerComments.LogicalRule(rule.Id.Value, variant.VariantIndex);

        artifact = FilterRuleArtifact.Create(
            ordinal,
            action,
            comment,
            matchers: normalized,
            actionParameters: actionParameters,
            logicalRuleId: rule.Id.Value,
            variantIndex: (uint)variant.VariantIndex,
            log: rule.Logging.Enabled,
            logPrefix: rule.Logging.Prefix);
        return null;
    }
}
