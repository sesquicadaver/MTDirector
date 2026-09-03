using System.Security.Cryptography.X509Certificates;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Inventory;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Ports;

/// <summary>Builds API-SSL connect options from materialized connection profile data.</summary>
internal static class RouterOsApiSslConnectOptionsBuilder
{
    public static ApiSslConnectOptions Build(RouterOsConnectionMaterial material, SecretLease password)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(password);

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
            CertificateRevocationMode = material.TrustMode == CertificateTrustMode.InternalCa
                ? material.CertificateRevocationMode
                : X509RevocationMode.NoCheck,
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
