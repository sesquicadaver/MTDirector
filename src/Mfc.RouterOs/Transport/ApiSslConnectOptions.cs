using System.Security.Cryptography.X509Certificates;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.RouterOs.Transport;

/// <summary>Parameters for an authenticated API-SSL connection (port 8729 by default).</summary>
public sealed class ApiSslConnectOptions
{
    /// <summary>Only API-SSL port is accepted in production connect path.</summary>
    public const ushort ApiSslPort = 8729;

    public required string Host { get; init; }

    public ushort Port { get; init; } = ApiSslPort;

    public required string Username { get; init; }

    /// <summary>Caller-owned lease; disposed by the connect path after login flush.</summary>
    public required SecretLease Password { get; init; }

    public required CertificateTrustMode TrustMode { get; init; }

    /// <summary>Trusted internal CA certificates for <see cref="CertificateTrustMode.InternalCa"/>.</summary>
    public X509Certificate2Collection? TrustedRootCertificates { get; init; }

    /// <summary>Expected SPKI SHA-256 for <see cref="CertificateTrustMode.SpkiPin"/>.</summary>
    public Hash256? PinnedSpkiSha256 { get; init; }

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan TlsAndLoginTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public Session.RosSessionOptions? SessionOptions { get; init; }
}
