namespace Mfc.RouterOs.Commands;

/// <summary>
/// Compile-time on-demand neighbor-discovery read allowlist (#314).
/// Covers RouterOS <c>/ip/neighbor/print</c> only — not IPv6 ND / ARP attribution (M7.2).
/// </summary>
public static class NeighborDiscoveryAllowlist
{
    private static readonly RosReadCommandId[] CommandSet =
    [
        RosReadCommandId.IpNeighbors,
    ];

    public static IReadOnlyList<RosReadCommandId> CommandIds => CommandSet;

    public static IReadOnlyList<string> FixedPaths { get; } =
    [
        "/ip/neighbor/print",
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
