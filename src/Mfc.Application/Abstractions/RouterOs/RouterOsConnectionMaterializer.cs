using Mfc.Application.Abstractions.Secrets;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Abstractions.RouterOs;

/// <summary>Resolves encrypted connection profile material for a <see cref="RouterOsReadTarget"/>.</summary>
public interface IRouterOsConnectionMaterializer
{
    Task<RouterOsConnectionMaterial> MaterializeAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default);
}

/// <summary>Trusted internal CA certificates keyed by <c>CaProfileRef</c>.</summary>
public interface IRouterOsTrustedCaStore
{
    IReadOnlyList<byte[]> GetCertificateDerBytes(string caProfileRef);
}

/// <summary>Short-lived connect parameters with decrypted password lease.</summary>
public sealed class RouterOsConnectionMaterial : IDisposable
{
    public required string Host { get; init; }

    public ushort Port { get; init; }

    public required string Username { get; init; }

    public required SecretLease Password { get; init; }

    public CertificateTrustMode TrustMode { get; init; }

    public Hash256? PinnedSpkiSha256 { get; init; }

    public IReadOnlyList<byte[]> TrustedCaCertificatesDer { get; init; } = [];

    public int ConnectTimeoutMs { get; init; }

    public int CommandTimeoutMs { get; init; }

    public void Dispose() => Password.Dispose();
}
