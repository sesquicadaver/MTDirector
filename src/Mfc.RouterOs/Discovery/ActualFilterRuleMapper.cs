using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps M1-13 filter discovery rows onto Domain actual-filter rules, including dynamic
/// rows and unknown matchers that canonical configuration sections omit.
/// </summary>
public static class ActualFilterRuleMapper
{
    public static IReadOnlyList<ActualFilterRule> FromDiscovery(FirewallFilterDiscoveryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        List<ActualFilterRule> rules = new(result.Ipv4FilterRules.Count + result.Ipv6FilterRules.Count);
        Append(rules, result.Ipv4FilterRules, IpAddressFamily.IPv4);
        Append(rules, result.Ipv6FilterRules, IpAddressFamily.IPv6);
        return rules;
    }

    private static void Append(
        List<ActualFilterRule> target,
        IReadOnlyList<FirewallFilterRuleDiscovery> source,
        IpAddressFamily family)
    {
        foreach (FirewallFilterRuleDiscovery rule in source)
        {
            Dictionary<string, string> known = new(StringComparer.Ordinal);
            Dictionary<string, string> unknown = new(StringComparer.Ordinal);
            ActualFilterMatchers.Partition(rule.KnownProperties, known, unknown);
            ActualFilterMatchers.Partition(rule.RawProperties, known, unknown);

            target.Add(ActualFilterRule.Create(
                family,
                string.IsNullOrWhiteSpace(rule.Chain) ? "forward" : rule.Chain,
                rule.EffectiveOrdinal,
                rule.Action,
                disabled: IsTruthy(rule.Disabled),
                dynamic: rule.IsDynamic,
                jumpTarget: rule.JumpTarget,
                comment: rule.Comment,
                knownMatchers: known,
                unknownMatchers: unknown));
        }
    }

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
