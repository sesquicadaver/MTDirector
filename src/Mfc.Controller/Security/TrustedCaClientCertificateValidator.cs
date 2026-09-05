using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Mfc.Controller.Security;

/// <summary>
/// Validates inbound mTLS client certificates against INTERNAL_CA TrustedCa roots (W7-04).
/// CustomRootTrust + configured revocation; clientAuth EKU when present.
/// </summary>
public static class TrustedCaClientCertificateValidator
{
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";

    /// <summary>
    /// Kestrel <c>ClientCertificateValidation</c> predicate.
    /// Null certificate is allowed only for <see cref="ClientCertificateMode.AllowCertificate"/>.
    /// </summary>
    public static bool Validate(
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors,
        ClientCertificateMode mode,
        IReadOnlyList<X509Certificate2> trustedRoots,
        X509RevocationMode revocationMode)
    {
        _ = sslPolicyErrors;
        _ = chain;

        if (certificate is null)
        {
            return mode == ClientCertificateMode.AllowCertificate;
        }

        if (trustedRoots is null || trustedRoots.Count == 0)
        {
            return false;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
        {
            return false;
        }

        if (!AllowsClientAuth(certificate))
        {
            return false;
        }

        using X509Chain custom = new();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        foreach (X509Certificate2 root in trustedRoots)
        {
            custom.ChainPolicy.CustomTrustStore.Add(root);
        }

        custom.ChainPolicy.RevocationMode = revocationMode;
        custom.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        custom.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        return custom.Build(certificate);
    }

    /// <summary>Loads DER roots from store material into disposable-owned certificates.</summary>
    public static IReadOnlyList<X509Certificate2> LoadTrustedRoots(IReadOnlyList<byte[]> derCertificates)
    {
        ArgumentNullException.ThrowIfNull(derCertificates);
        if (derCertificates.Count == 0)
        {
            return [];
        }

        List<X509Certificate2> roots = new(derCertificates.Count);
        foreach (byte[] der in derCertificates)
        {
            roots.Add(X509CertificateLoader.LoadCertificate(der));
        }

        return roots;
    }

    /// <summary>
    /// clientAuth EKU required when EKU extension is present; missing EKU treated as unrestricted (lab PFX).
    /// </summary>
    internal static bool AllowsClientAuth(X509Certificate2 certificate)
    {
        bool sawEku = false;
        foreach (X509Extension extension in certificate.Extensions)
        {
            if (extension is not X509EnhancedKeyUsageExtension eku)
            {
                continue;
            }

            sawEku = true;
            foreach (Oid oid in eku.EnhancedKeyUsages)
            {
                if (oid.Value == ClientAuthOid)
                {
                    return true;
                }
            }
        }

        return !sawEku;
    }
}
