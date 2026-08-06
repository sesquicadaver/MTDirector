using System.Net;
using Microsoft.Extensions.Hosting;

namespace Mfc.Controller.Configuration;

/// <summary>
/// Fails fast on illegal Controller host configuration (TLS, bind, development auth).
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
        }

        if (options.Grpc.ShutdownTimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException("Mfc:Grpc:ShutdownTimeoutSeconds must be between 1 and 600.");
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
}
