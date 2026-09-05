using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>
/// Resolves <c>x-mfc-actor</c> for Desktop→Controller gRPC (W7-05).
/// When a client certificate is configured, the cert CN is the actor (cert-bound identity).
/// Otherwise falls back to <see cref="DesktopOptions.Actor"/> / <c>desktop</c>.
/// </summary>
public static class DesktopGrpcActorResolver
{
    public const string MetadataKey = "x-mfc-actor";
    public const string DefaultActor = "desktop";

    private static readonly ConcurrentDictionary<string, string> ActorByCertificateKey =
        new(StringComparer.Ordinal);

    /// <summary>Resolves the actor string used in gRPC metadata.</summary>
    public static string Resolve(DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ClientCertificatePath))
        {
            return FallbackActor(options);
        }

        string cacheKey = BuildCacheKey(options);
        return ActorByCertificateKey.GetOrAdd(cacheKey, _ => DeriveFromClientCertificate(options));
    }

    /// <summary>Builds request metadata with the resolved actor.</summary>
    public static Metadata CreateHeaders(DesktopOptions options)
        => new() { { MetadataKey, Resolve(options) } };

    /// <summary>Test helper: clears the cert→actor cache.</summary>
    internal static void ClearCache() => ActorByCertificateKey.Clear();

    private static string DeriveFromClientCertificate(DesktopOptions options)
    {
        using X509Certificate2? cert = DesktopGrpcHttpHandlerFactory.TryLoadClientCertificate(options);
        if (cert is null)
        {
            return FallbackActor(options);
        }

        string? cn = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        if (!string.IsNullOrWhiteSpace(cn))
        {
            return cn.Trim();
        }

        return FallbackActor(options);
    }

    private static string FallbackActor(DesktopOptions options)
        => string.IsNullOrWhiteSpace(options.Actor) ? DefaultActor : options.Actor.Trim();

    private static string BuildCacheKey(DesktopOptions options)
        => options.ClientCertificatePath!.Trim()
           + "\u001f"
           + (options.ClientCertificatePassword ?? string.Empty);
}
