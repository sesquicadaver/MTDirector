using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>Maps live managed state into Domain observation inputs (SEC-02).</summary>
public static class ManagedResourceHashObservation
{
    public static bool TryComputeFromManagedState(
        RouterOsFilterArtifact expected,
        ActualManagedState state,
        out Hash256 observedHash,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(state);

        List<ActualAddressListEntry> lists = [];
        lists.AddRange(MapAddressLists(IpAddressFamily.IPv4, state.Ipv4AddressLists));
        lists.AddRange(MapAddressLists(IpAddressFamily.IPv6, state.Ipv6AddressLists));

        List<ActualFilterChainRule> rules = [];
        rules.AddRange(MapFilterRules(state.Ipv4FilterRules));
        rules.AddRange(MapFilterRules(state.Ipv6FilterRules));

        Dictionary<string, string> jumps = ExtractAnchorJumps(state);
        return ObservedManagedResourceHash.TryCompute(
            expected,
            lists,
            rules,
            jumps,
            out observedHash,
            out error);
    }

    public static Dictionary<string, string> ExtractAnchorJumps(ActualManagedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Dictionary<string, string> jumps = new(StringComparer.Ordinal);
        foreach (IReadOnlyDictionary<string, string> row in state.Ipv4FilterRules.Concat(state.Ipv6FilterRules))
        {
            string? comment = row.GetValueOrDefault("comment");
            string? action = row.GetValueOrDefault("action");
            string? jump = row.GetValueOrDefault("jump-target");
            if (string.IsNullOrWhiteSpace(comment)
                || !string.Equals(action, "jump", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(jump))
            {
                continue;
            }

            if (comment.StartsWith("mfc:anchor:", StringComparison.Ordinal)
                || comment.StartsWith("fwc:anchor:", StringComparison.Ordinal))
            {
                jumps[comment] = jump.Trim();
            }
        }

        return jumps;
    }

    private static List<ActualAddressListEntry> MapAddressLists(
        IpAddressFamily family,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        _ = family;
        List<ActualAddressListEntry> mapped = new(rows.Count);
        foreach (IReadOnlyDictionary<string, string> row in rows)
        {
            string? list = row.GetValueOrDefault("list");
            string? address = row.GetValueOrDefault("address");
            if (string.IsNullOrWhiteSpace(list) || string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            mapped.Add(new ActualAddressListEntry(
                list,
                address,
                dynamic: Yes(row.GetValueOrDefault("dynamic")),
                timeout: row.GetValueOrDefault("timeout"),
                comment: row.GetValueOrDefault("comment"),
                disabled: Yes(row.GetValueOrDefault("disabled"))));
        }

        return mapped;
    }

    private static List<ActualFilterChainRule> MapFilterRules(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        List<ActualFilterChainRule> mapped = new(rows.Count);
        foreach (IReadOnlyDictionary<string, string> row in rows)
        {
            string? chain = row.GetValueOrDefault("chain");
            string? action = row.GetValueOrDefault("action");
            if (string.IsNullOrWhiteSpace(chain) || string.IsNullOrWhiteSpace(action))
            {
                continue;
            }

            mapped.Add(new ActualFilterChainRule(
                chain,
                action,
                comment: row.GetValueOrDefault("comment"),
                disabled: Yes(row.GetValueOrDefault("disabled")),
                invalid: Yes(row.GetValueOrDefault("invalid")),
                dynamic: Yes(row.GetValueOrDefault("dynamic")),
                log: Yes(row.GetValueOrDefault("log")),
                logPrefix: row.GetValueOrDefault("log-prefix"),
                properties: new Dictionary<string, string>(row, StringComparer.Ordinal)));
        }

        return mapped;
    }

    private static bool Yes(string? value)
        => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
