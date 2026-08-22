namespace Mfc.RouterOs.Commands;

/// <summary>
/// Compile-time endpoint-attribution read allowlist (M7.2-01 / next-2 §3).
/// Covers ARP/ND, DHCP, bridge host, DHCP snooping, VPN sessions; no routing/firewall writes.
/// </summary>
public static class EndpointAttributionAllowlist
{
    private static readonly RosReadCommandId[] CommandSet =
    [
        RosReadCommandId.Ipv4Arp,
        RosReadCommandId.Ipv6Neighbors,
        RosReadCommandId.DhcpServerLeases,
        RosReadCommandId.BridgeHosts,
        RosReadCommandId.DhcpSnoopingBindings,
        RosReadCommandId.WireGuardPeers,
        RosReadCommandId.IpsecActivePeers,
        RosReadCommandId.PppActiveSessions,
    ];

    public static IReadOnlyList<RosReadCommandId> CommandIds => CommandSet;

    public static IReadOnlyList<string> FixedPaths { get; } =
    [
        "/ip/arp/print",
        "/ipv6/neighbor/print",
        "/ip/dhcp-server/lease/print",
        "/interface/bridge/host/print",
        "/interface/bridge/dhcp-snooping/binding/print",
        "/interface/wireguard/peers/print",
        "/ip/ipsec/active-peers/print",
        "/ppp/active/print",
    ];

    /// <summary>Property names that must never appear on endpoint-attribution allowlist profiles.</summary>
    public static IReadOnlyList<string> ForbiddenPropertyNames { get; } =
    [
        "password",
        "secret",
        "passphrase",
        "private-key",
        "psk",
        "shared-key",
        "auth-key",
        "token",
        "api-key",
        "public-key",
    ];
}
