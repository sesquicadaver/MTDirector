namespace Mfc.RouterOs.Commands;

/// <summary>
/// Compile-time on-demand connection-tracking read allowlist (M7.3-03 / next-2 §2).
/// Read-only connection print paths; no full-table persistence in Controller.
/// </summary>
public static class ConnectionTrackingAllowlist
{
    private static readonly RosReadCommandId[] CommandSet =
    [
        RosReadCommandId.Ipv4FirewallConnections,
        RosReadCommandId.Ipv6FirewallConnections,
    ];

    public static IReadOnlyList<RosReadCommandId> CommandIds => CommandSet;

    public static IReadOnlyList<string> FixedPaths { get; } =
    [
        "/ip/firewall/connection/print",
        "/ipv6/firewall/connection/print",
    ];

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
