using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;

namespace Mfc.Controller.Configuration;

/// <summary>
/// Fails fast on illegal Controller host configuration (TLS, bind, development auth, database, master key).
/// </summary>
public static class ControllerOptionsValidator
{
    public static void Validate(ControllerOptions options, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Grpc.ListenAddress))
        {
            throw new InvalidOperationException("Mfc:Grpc:ListenAddress is required.");
        }

        if (!Uri.TryCreate(options.Grpc.ListenAddress, UriKind.Absolute, out Uri? listenUri))
        {
            throw new InvalidOperationException($"Mfc:Grpc:ListenAddress is not a valid absolute URI: '{options.Grpc.ListenAddress}'.");
        }

        if (listenUri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"Mfc:Grpc:ListenAddress scheme must be http or https: '{listenUri.Scheme}'.");
        }

        bool isDevelopment = string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);
        bool isLoopback = IsLoopback(listenUri);
        bool isHttps = string.Equals(listenUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(options.Database.ConnectionString))
        {
            throw new InvalidOperationException("Mfc:Database:ConnectionString is required.");
        }

        if (ContainsSqlite(options.Database.ConnectionString))
        {
            throw new InvalidOperationException("SQLite is not a supported production database. Use PostgreSQL.");
        }

        if (string.IsNullOrWhiteSpace(options.Security.MasterKeyProvider))
        {
            throw new InvalidOperationException("Mfc:Security:MasterKeyProvider is required.");
        }

        ValidateTrustedCa(options.Security.TrustedCa);

        if (!isDevelopment)
        {
            if (options.Grpc.AllowInsecureLoopback)
            {
                throw new InvalidOperationException("Mfc:Grpc:AllowInsecureLoopback is forbidden outside Development.");
            }

            if (options.Authentication.AllowDevelopmentAuthentication)
            {
                throw new InvalidOperationException("Development authentication is forbidden outside Development.");
            }

            if (options.Authentication.AllowMetadataActor)
            {
                throw new InvalidOperationException(
                    "Mfc:Authentication:AllowMetadataActor is forbidden outside Development.");
            }

            if (string.Equals(options.Security.MasterKeyProvider, "Development", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Development master-key provider is forbidden outside Development.");
            }

            if (!isHttps || !options.Security.RequireTls)
            {
                throw new InvalidOperationException("Production bind without TLS is blocked. Use https:// and Mfc:Security:RequireTls=true.");
            }
        }
        else
        {
            if (!isHttps)
            {
                if (!options.Grpc.AllowInsecureLoopback || !isLoopback)
                {
                    throw new InvalidOperationException(
                        "Development HTTP bind requires loopback and Mfc:Grpc:AllowInsecureLoopback=true.");
                }
            }

            if (options.Authentication.AllowDevelopmentAuthentication && !isLoopback)
            {
                throw new InvalidOperationException(
                    "Development authentication is allowed only when gRPC bind is loopback.");
            }

            if (options.Authentication.AllowMetadataActor && !isLoopback)
            {
                throw new InvalidOperationException(
                    "AllowMetadataActor is allowed only when gRPC bind is loopback.");
            }
        }

        if (options.Grpc.ShutdownTimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException("Mfc:Grpc:ShutdownTimeoutSeconds must be between 1 and 600.");
        }

        ClientCertificateMode clientCertMode = GrpcClientCertificateModeParser.Parse(options.Grpc.ClientCertificateMode);
        if (!isHttps && clientCertMode != ClientCertificateMode.NoCertificate)
        {
            throw new InvalidOperationException(
                "Mfc:Grpc:ClientCertificateMode other than NoCertificate requires an https:// ListenAddress.");
        }

        if (GrpcClientCertificateModeParser.RequestsOrAllowsClientCertificate(clientCertMode))
        {
            if (string.IsNullOrWhiteSpace(options.Security.TrustedCa.ProfilesDirectory))
            {
                throw new InvalidOperationException(
                    "Mfc:Security:TrustedCa:ProfilesDirectory is required when ClientCertificateMode allows or requires client certificates.");
            }

            if (string.IsNullOrWhiteSpace(options.Security.TrustedCa.ClientCaProfileRef))
            {
                throw new InvalidOperationException(
                    "Mfc:Security:TrustedCa:ClientCaProfileRef is required when ClientCertificateMode allows or requires client certificates.");
            }

            ValidateClientCaProfileRef(options.Security.TrustedCa.ClientCaProfileRef);
        }
    }

    public static bool IsLoopback(Uri listenUri)
    {
        if (string.Equals(listenUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(listenUri.Host, out IPAddress? address))
        {
            return IPAddress.IsLoopback(address);
        }

        return false;
    }

    private static void ValidateClientCaProfileRef(string? profileRef)
    {
        string key = profileRef?.Trim() ?? string.Empty;
        if (key.Length == 0)
        {
            return;
        }

        if (key.Contains("..", StringComparison.Ordinal)
            || key.Contains('/', StringComparison.Ordinal)
            || key.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(key))
        {
            throw new InvalidOperationException(
                $"Invalid Mfc:Security:TrustedCa:ClientCaProfileRef '{profileRef}'.");
        }
    }

    private static void ValidateTrustedCa(TrustedCaHostOptions trustedCa)
    {
        ArgumentNullException.ThrowIfNull(trustedCa);
        string mode = trustedCa.RevocationMode?.Trim() ?? string.Empty;
        if (mode.Length == 0)
        {
            throw new InvalidOperationException("Mfc:Security:TrustedCa:RevocationMode is required.");
        }

        if (!string.Equals(mode, "Online", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "Offline", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "NoCheck", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unknown Mfc:Security:TrustedCa:RevocationMode '{trustedCa.RevocationMode}'. Supported: Online, Offline, NoCheck.");
        }

        if (!string.IsNullOrWhiteSpace(trustedCa.ProfilesDirectory)
            && !Path.IsPathRooted(trustedCa.ProfilesDirectory.Trim()))
        {
            throw new InvalidOperationException(
                "Mfc:Security:TrustedCa:ProfilesDirectory must be an absolute path when set.");
        }

        if (!string.IsNullOrWhiteSpace(trustedCa.ClientCaProfileRef))
        {
            ValidateClientCaProfileRef(trustedCa.ClientCaProfileRef);
        }
    }

    private static bool ContainsSqlite(string connectionString)
        => connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
           || connectionString.Contains("Filename=", StringComparison.OrdinalIgnoreCase)
           || connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase);
}
