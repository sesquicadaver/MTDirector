using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Application.Policies;

/// <summary>
/// Maps canonical firewall filter records (M1-22) onto Domain actual-filter rules (M2-12).
/// Does not call RouterOS; snapshot sections are already Contracts/Domain canonical.
/// </summary>
public static class ActualFilterContextMapper
{
    public static ActualFilterAnalysisResult Analyze(
        IReadOnlyList<CanonicalRecord> ipv4Filter,
        IReadOnlyList<CanonicalRecord> ipv6Filter,
        ChainContractSet contracts)
    {
        ArgumentNullException.ThrowIfNull(ipv4Filter);
        ArgumentNullException.ThrowIfNull(ipv6Filter);
        ArgumentNullException.ThrowIfNull(contracts);
        List<ActualFilterRule> rules = [];
        rules.AddRange(FromCanonicalFilter(IpAddressFamily.IPv4, ipv4Filter));
        rules.AddRange(FromCanonicalFilter(IpAddressFamily.IPv6, ipv6Filter));
        return ActualFilterAnalysis.Analyze(rules, contracts);
    }

    public static IReadOnlyList<ActualFilterRule> FromCanonicalFilter(
        IpAddressFamily family,
        IReadOnlyList<CanonicalRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        List<ActualFilterRule> rules = new(records.Count);
        for (int i = 0; i < records.Count; i++)
        {
            IReadOnlyDictionary<string, string> properties = records[i].Properties;
            int ordinal = i;
            if (properties.TryGetValue("ordinal", out string? ordinalText)
                && int.TryParse(ordinalText, out int parsed))
            {
                ordinal = parsed;
            }

            Dictionary<string, string> known = new(StringComparer.Ordinal);
            Dictionary<string, string> unknown = new(StringComparer.Ordinal);
            ActualFilterMatchers.Partition(properties, known, unknown);
            rules.Add(ActualFilterRule.Create(
                family,
                Get(properties, "chain") ?? "forward",
                ordinal,
                Get(properties, "action"),
                disabled: IsTruthy(Get(properties, "disabled")),
                dynamic: IsTruthy(Get(properties, "dynamic")),
                jumpTarget: Get(properties, "jump-target"),
                comment: Get(properties, "comment"),
                knownMatchers: known,
                unknownMatchers: unknown));
        }

        return rules;
    }

    private static string? Get(IReadOnlyDictionary<string, string> properties, string key)
        => properties.TryGetValue(key, out string? value) ? value : null;

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
