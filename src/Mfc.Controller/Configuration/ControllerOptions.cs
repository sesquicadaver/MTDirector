using System.ComponentModel.DataAnnotations;

namespace Mfc.Controller.Configuration;

/// <summary>
/// Root controller configuration bound from section <c>Mfc</c> / env prefix <c>MFC__</c>.
/// </summary>
public sealed class ControllerOptions
{
    public const string SectionName = "Mfc";

    [Required]
    public GrpcHostOptions Grpc { get; init; } = new();

    [Required]
    public SecurityHostOptions Security { get; init; } = new();

    [Required]
    public AuthenticationHostOptions Authentication { get; init; } = new();
}

public sealed class GrpcHostOptions
{
    /// <summary>Kestrel URL, e.g. https://127.0.0.1:5101 or http://127.0.0.1:5101 (dev loopback only).</summary>
    [Required]
    [MinLength(1)]
    public string ListenAddress { get; init; } = "https://127.0.0.1:5101";

    /// <summary>Graceful shutdown deadline for draining in-flight requests.</summary>
    [Range(1, 600)]
    public int ShutdownTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// When true (Development only), allows http:// on loopback without TLS.
    /// Production must never enable this.
    /// </summary>
    public bool AllowInsecureLoopback { get; init; }
}

public sealed class SecurityHostOptions
{
    /// <summary>When true, non-HTTPS binds are rejected (always required outside Development insecure-loopback mode).</summary>
    public bool RequireTls { get; init; } = true;
}

public sealed class AuthenticationHostOptions
{
    /// <summary>
    /// Development-only authentication shortcut. Requires Development + loopback bind + this flag.
    /// </summary>
    public bool AllowDevelopmentAuthentication { get; init; }
}
