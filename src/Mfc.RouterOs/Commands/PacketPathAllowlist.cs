namespace Mfc.RouterOs.Commands;

/// <summary>
/// Compile-time packet-path read allowlist surface (N1-01).
/// Topology projection and path classification belong to later N1 issues.
/// </summary>
public static class PacketPathAllowlist
{
    private static readonly RosReadCommandId[] CommandSet =
    [
        RosReadCommandId.Containers,
        RosReadCommandId.Apps,
        RosReadCommandId.VethInterfaces,
        RosReadCommandId.IpVrfs,
    ];

    public static IReadOnlyList<RosReadCommandId> CommandIds => CommandSet;

    public static IReadOnlyList<string> FixedPaths { get; } =
    [
        "/container/print",
        "/app/print",
        "/interface/veth/print",
        "/ip/vrf/print",
    ];

    /// <summary>Property names that must never appear on N1 packet-path allowlist profiles.</summary>
    public static IReadOnlyList<string> ForbiddenPropertyNames { get; } =
    [
        "env",
        "envs",
        "envlist",
        "mount",
        "mounts",
        "mountlists",
        "cmd",
        "entrypoint",
        "workdir",
        "file",
        "root-dir",
        "logging",
        "devices",
        "user",
        "yaml",
        "password",
        "secret",
    ];
}
