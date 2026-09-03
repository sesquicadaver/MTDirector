using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Mfc.Infrastructure.RouterOs;

/// <summary>
/// Loads INTERNAL_CA roots from <c>{ProfilesDirectory}/{caProfileRef}/*.{pem,crt,cer,der}</c> (SEC-04).
/// Missing profile or empty directory → empty list (materializer fail-closed).
/// </summary>
public sealed class DirectoryRouterOsTrustedCaStore : IRouterOsTrustedCaStore
{
    private static readonly HashSet<string> CertificateExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pem",
        ".crt",
        ".cer",
        ".der",
    };

    private readonly TrustedCaStoreOptions _options;
    private readonly ConcurrentDictionary<string, IReadOnlyList<byte[]>> _cache = new(StringComparer.Ordinal);

    public DirectoryRouterOsTrustedCaStore(IOptions<TrustedCaStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? new TrustedCaStoreOptions();
    }

    /// <summary>Test helper: construct without options wrapper.</summary>
    public DirectoryRouterOsTrustedCaStore(TrustedCaStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public IReadOnlyList<byte[]> GetCertificateDerBytes(string caProfileRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caProfileRef);
        string key = caProfileRef.Trim();
        if (key.Contains("..", StringComparison.Ordinal)
            || key.Contains('/', StringComparison.Ordinal)
            || key.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(key))
        {
            throw new InvalidOperationException($"Invalid CaProfileRef '{caProfileRef}'.");
        }

        return _cache.GetOrAdd(key, LoadProfile);
    }

    private IReadOnlyList<byte[]> LoadProfile(string caProfileRef)
    {
        string? root = _options.ProfilesDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            return [];
        }

        string rootFull = Path.GetFullPath(root.Trim());
        string profileDir = Path.GetFullPath(Path.Combine(rootFull, caProfileRef));
        string relative = Path.GetRelativePath(rootFull, profileDir);
        if (relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            || !Directory.Exists(profileDir))
        {
            return [];
        }

        List<byte[]> derList = [];
        foreach (string path in Directory.EnumerateFiles(profileDir)
                     .OrderBy(static p => p, StringComparer.Ordinal))
        {
            if (!CertificateExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            byte[] raw = File.ReadAllBytes(path);
            using X509Certificate2 cert = LoadCertificate(raw, path);
            derList.Add(cert.Export(X509ContentType.Cert));
        }

        return derList;
    }

    private static X509Certificate2 LoadCertificate(byte[] raw, string path)
    {
        try
        {
            return X509CertificateLoader.LoadCertificate(raw);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Trusted CA file '{path}' is not a valid X.509 certificate.",
                ex);
        }
    }
}
