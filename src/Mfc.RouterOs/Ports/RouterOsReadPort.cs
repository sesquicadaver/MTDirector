using System.Security.Cryptography.X509Certificates;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Inventory;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Ports;

/// <summary>
/// Production <see cref="IRouterOsReadPort"/> — API-SSL identity probe via allowlisted system discovery (P2-04 / M1-09…M1-11).
/// </summary>
public sealed class RouterOsReadPort : IRouterOsReadPort
{
    private readonly IRouterOsConnectionMaterializer _materializer;

    public RouterOsReadPort(IRouterOsConnectionMaterializer materializer)
    {
        ArgumentNullException.ThrowIfNull(materializer);
        _materializer = materializer;
    }

    /// <inheritdoc />
    public async Task<RouterOsProbeResult> ProbeAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        using RouterOsConnectionMaterial material = await _materializer
            .MaterializeAsync(target, cancellationToken)
            .ConfigureAwait(false);

        using Transport.SecretLease password = new(material.Password.Plaintext);

        ApiSslConnectOptions options = BuildConnectOptions(material, password);
        try
        {
            await using AuthenticatedRosConnection connection = await AuthenticatedRosConnection
                .ConnectAsync(options, cancellationToken)
                .ConfigureAwait(false);

            return await RouterOsSystemProbe
                .ProbeAsync(connection.Session, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiSslException ex)
        {
            throw new InvalidOperationException($"RouterOS API-SSL probe failed: {ex.Code}.", ex);
        }
    }

    private static ApiSslConnectOptions BuildConnectOptions(
        RouterOsConnectionMaterial material,
        Transport.SecretLease password)
    {
        X509Certificate2Collection? trustedRoots = null;
        if (material.TrustMode == CertificateTrustMode.InternalCa)
        {
            trustedRoots = new X509Certificate2Collection();
            foreach (byte[] der in material.TrustedCaCertificatesDer)
            {
                trustedRoots.Add(X509CertificateLoader.LoadCertificate(der));
            }
        }

        TimeSpan connectTimeout = TimeSpan.FromMilliseconds(
            Math.Clamp(material.ConnectTimeoutMs, DeviceConnectionProfile.MinConnectTimeoutMs, DeviceConnectionProfile.MaxConnectTimeoutMs));
        TimeSpan commandTimeout = TimeSpan.FromMilliseconds(
            Math.Clamp(material.CommandTimeoutMs, DeviceConnectionProfile.MinCommandTimeoutMs, DeviceConnectionProfile.MaxCommandTimeoutMs));

        return new ApiSslConnectOptions
        {
            Host = material.Host,
            Port = material.Port,
            Username = material.Username,
            Password = password,
            TrustMode = material.TrustMode,
            TrustedRootCertificates = trustedRoots,
            PinnedSpkiSha256 = material.PinnedSpkiSha256,
            ConnectTimeout = connectTimeout,
            TlsAndLoginTimeout = TimeSpan.FromTicks(connectTimeout.Ticks + commandTimeout.Ticks),
            SessionOptions = new RosSessionOptions
            {
                DefaultCommandTimeout = commandTimeout,
            },
        };
    }
}
