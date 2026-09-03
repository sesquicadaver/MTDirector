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

    [Required]
    public DatabaseHostOptions Database { get; init; } = new();
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

    /// <summary>
    /// HTTPS client-certificate mode (W7-03): <c>NoCertificate</c>, <c>AllowCertificate</c>, or <c>RequireCertificate</c>.
    /// Applied via Kestrel <c>ConfigureHttpsDefaults</c>. HTTP binds ignore this (must stay NoCertificate).
    /// </summary>
    public string ClientCertificateMode { get; init; } = "NoCertificate";
}

public sealed class SecurityHostOptions
{
    /// <summary>When true, non-HTTPS binds are rejected (always required outside Development insecure-loopback mode).</summary>
    public bool RequireTls { get; init; } = true;

    /// <summary>
    /// Named master-key provider (e.g. OS key store). Required; Development provider forbidden outside Development.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string MasterKeyProvider { get; init; } = string.Empty;

    /// <summary>INTERNAL_CA trusted roots + revocation policy (SEC-04).</summary>
    public TrustedCaHostOptions TrustedCa { get; init; } = new();
}

/// <summary>Directory layout and revocation mode for INTERNAL_CA profiles.</summary>
public sealed class TrustedCaHostOptions
{
    /// <summary>
    /// Root directory with one subdirectory per <c>CaProfileRef</c> containing PEM/DER/CRT/CER files.
    /// Empty keeps the store fail-closed until operators configure profiles.
    /// </summary>
    public string? ProfilesDirectory { get; init; }

    /// <summary>Online (default), Offline, or NoCheck. Production should keep Online/Offline with CRL/OCSP material.</summary>
    public string RevocationMode { get; init; } = "Online";
}

public sealed class AuthenticationHostOptions
{
    /// <summary>
    /// Development-only authentication shortcut. Requires Development + loopback bind + this flag.
    /// </summary>
    public bool AllowDevelopmentAuthentication { get; init; }

    /// <summary>
    /// Documents that Development may resolve actor from <c>x-mfc-actor</c> when no principal is present (W7-02).
    /// Forbidden outside Development — Production must bind actor to TLS/auth principal (see <c>GrpcRequestActorResolver</c>).
    /// </summary>
    public bool AllowMetadataActor { get; init; }
}

public sealed class DatabaseHostOptions
{
    /// <summary>PostgreSQL connection string. Never log this value.</summary>
    [Required]
    [MinLength(1)]
    public string ConnectionString { get; init; } = string.Empty;
}
