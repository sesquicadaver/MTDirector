namespace Mfc.Infrastructure.Security;

/// <summary>Configuration for INTERNAL_CA profile material (SEC-04).</summary>
public sealed class TrustedCaStoreOptions
{
    public const string SectionPath = "Mfc:Security:TrustedCa";

    /// <summary>
    /// Root directory containing one subdirectory per <c>CaProfileRef</c>
    /// (PEM/DER/CRT/CER files). Empty/null keeps the store fail-closed (no roots).
    /// </summary>
    public string? ProfilesDirectory { get; init; }

    /// <summary>
    /// Revocation mode for INTERNAL_CA chain builds: <c>Online</c>, <c>Offline</c>, or <c>NoCheck</c>.
    /// Default <c>Online</c> (mandatory revocation policy).
    /// </summary>
    public string RevocationMode { get; init; } = "Online";

    /// <summary>
    /// CaProfileRef subdirectory under <see cref="ProfilesDirectory"/> used to validate inbound mTLS client certificates (W7-04).
    /// Required when <c>Mfc:Grpc:ClientCertificateMode</c> is AllowCertificate or RequireCertificate.
    /// </summary>
    public string? ClientCaProfileRef { get; init; }
}
