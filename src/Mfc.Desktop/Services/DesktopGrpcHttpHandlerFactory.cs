using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>
/// Builds the <see cref="SocketsHttpHandler"/> used for Desktop→Controller gRPC (W7-03).
/// Optional client certificate presentation when <see cref="DesktopOptions.ClientCertificatePath"/> is set.
/// </summary>
public static class DesktopGrpcHttpHandlerFactory
{
    /// <summary>Creates an HTTP/2 handler, attaching a client certificate when configured.</summary>
    public static SocketsHttpHandler Create(DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        SocketsHttpHandler handler = new()
        {
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(Math.Max(1, options.HealthCheckTimeoutSeconds)),
        };

        X509Certificate2? clientCert = TryLoadClientCertificate(options);
        if (clientCert is not null)
        {
            handler.SslOptions.LocalCertificateSelectionCallback =
                LocalCertificateSelectionCallback(clientCert);
            handler.SslOptions.ClientCertificates = [clientCert];
        }

        return handler;
    }

    /// <summary>Loads a PFX/PEM client certificate from Desktop options, or null when unset.</summary>
    public static X509Certificate2? TryLoadClientCertificate(DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ClientCertificatePath))
        {
            return null;
        }

        string path = options.ClientCertificatePath.Trim();
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Desktop client certificate file not found: '{path}'.",
                path);
        }

        string? password = string.IsNullOrEmpty(options.ClientCertificatePassword)
            ? null
            : options.ClientCertificatePassword;

        return X509CertificateLoader.LoadPkcs12FromFile(
            path,
            password,
            X509KeyStorageFlags.EphemeralKeySet);
    }

    private static LocalCertificateSelectionCallback LocalCertificateSelectionCallback(X509Certificate2 cert)
        => (_, _, _, _, _) => cert;
}
