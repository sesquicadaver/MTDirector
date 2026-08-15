namespace Mfc.Domain.Policy;

/// <summary>Splits RouterOS filter properties into identity, known matchers, and unknown matchers.</summary>
public static class ActualFilterMatchers
{
    private static readonly HashSet<string> IdentityOrAction = new(StringComparer.Ordinal)
    {
        ".id",
        "chain",
        "action",
        "disabled",
        "comment",
        "jump-target",
        "dynamic",
        "invalid",
        "ordinal",
        "log",
        "log-prefix",
        "reject-with",
        "hw-offload",
        "address-list",
        "address-list-timeout",
    };

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "protocol",
        "src-address",
        "dst-address",
        "src-address-list",
        "dst-address-list",
        "src-address-type",
        "dst-address-type",
        "src-port",
        "dst-port",
        "port",
        "in-interface",
        "out-interface",
        "in-interface-list",
        "out-interface-list",
        "in-bridge-port",
        "out-bridge-port",
        "in-bridge-port-list",
        "out-bridge-port-list",
        "src-mac-address",
        "connection-state",
        "connection-nat-state",
        "tcp-flags",
        "icmp-options",
        "ipsec-policy",
        "fragment",
        "ipv4-options",
        "ttl",
        "ipv6-header",
        "hop-limit",
    };

    public static bool IsIdentityOrAction(string key)
        => IdentityOrAction.Contains(key);

    public static bool IsKnownMatcher(string key)
        => Known.Contains(key);

    public static void Partition(
        IReadOnlyDictionary<string, string> properties,
        Dictionary<string, string> known,
        Dictionary<string, string> unknown)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(unknown);
        foreach (KeyValuePair<string, string> pair in properties)
        {
            if (IsIdentityOrAction(pair.Key) || string.IsNullOrEmpty(pair.Value))
            {
                continue;
            }

            if (IsKnownMatcher(pair.Key))
            {
                known[pair.Key] = pair.Value;
            }
            else
            {
                unknown[pair.Key] = pair.Value;
            }
        }
    }
}
