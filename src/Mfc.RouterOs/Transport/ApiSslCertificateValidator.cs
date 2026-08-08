using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.RouterOs.Transport;

/// <summary>Certificate validation for INTERNAL_CA and SPKI_PIN trust modes (Spec §14.2–14.3).</summary>
public static class ApiSslCertificateValidator
{
    public static bool Validate(
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors,
        ApiSslConnectOptions options,
        out ApiSslException? error)
    {
        error = null;
        if (certificate is null)
        {
            error = new ApiSslException(ApiSslErrors.HandshakeFailed, "Remote certificate is missing.");
            return false;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
        {
            error = new ApiSslException(
                ApiSslErrors.CertificateExpired,
                "Remote certificate is outside its validity interval.");
            return false;
        }

        if (!HasServerAuthEku(certificate))
        {
            error = new ApiSslException(
                ApiSslErrors.CertificateMismatch,
                "Remote certificate lacks serverAuth Extended Key Usage.");
            return false;
        }

        if (!MatchesTargetIdentity(certificate, options.Host))
        {
            error = new ApiSslException(
                ApiSslErrors.HostnameMismatch,
                "Remote certificate SAN does not match management host.");
            return false;
        }

        return options.TrustMode switch
        {
            CertificateTrustMode.InternalCa => ValidateInternalCa(certificate, chain, options, out error),
            CertificateTrustMode.SpkiPin => ValidateSpkiPin(certificate, options, out error),
            _ => FailUnknownTrust(out error),
        };
    }

    public static Hash256 ComputeSpkiSha256(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        byte[] spki = certificate.PublicKey.ExportSubjectPublicKeyInfo();
        return Hash256.Create(SHA256.HashData(spki));
    }

    private static bool ValidateInternalCa(
        X509Certificate2 certificate,
        X509Chain? chain,
        ApiSslConnectOptions options,
        out ApiSslException? error)
    {
        error = null;
        if (options.TrustedRootCertificates is null || options.TrustedRootCertificates.Count == 0)
        {
            error = new ApiSslException(
                ApiSslErrors.CertificateMismatch,
                "INTERNAL_CA trust requires at least one trusted root certificate.");
            return false;
        }

        using X509Chain custom = new();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        custom.ChainPolicy.CustomTrustStore.AddRange(options.TrustedRootCertificates);
        custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        custom.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        if (!custom.Build(certificate))
        {
            error = new ApiSslException(
                ApiSslErrors.CertificateMismatch,
                "Certificate chain is not trusted by the configured internal CA.");
            return false;
        }

        _ = chain;
        return true;
    }

    private static bool ValidateSpkiPin(
        X509Certificate2 certificate,
        ApiSslConnectOptions options,
        out ApiSslException? error)
    {
        error = null;
        if (options.PinnedSpkiSha256 is null)
        {
            error = new ApiSslException(
                ApiSslErrors.CertificateMismatch,
                "SPKI_PIN trust requires a configured pin.");
            return false;
        }

        Hash256 actual = ComputeSpkiSha256(certificate);
        if (!actual.Equals(options.PinnedSpkiSha256))
        {
            error = new ApiSslException(
                ApiSslErrors.CertificateMismatch,
                "Remote certificate SPKI SHA-256 does not match the configured pin.");
            return false;
        }

        return true;
    }

    private static bool FailUnknownTrust(out ApiSslException? error)
    {
        error = new ApiSslException(ApiSslErrors.CertificateMismatch, "Unsupported certificate trust mode.");
        return false;
    }

    private static bool HasServerAuthEku(X509Certificate2 certificate)
    {
        foreach (X509Extension extension in certificate.Extensions)
        {
            if (extension is X509EnhancedKeyUsageExtension eku)
            {
                foreach (Oid oid in eku.EnhancedKeyUsages)
                {
                    if (oid.Value == "1.3.6.1.5.5.7.3.1")
                    {
                        return true;
                    }
                }
            }
        }

        // Certificates without EKU extension are treated as unrestricted by many stacks;
        // RouterOS API-SSL requires serverAuth — reject missing EKU.
        return false;
    }

    private static bool MatchesTargetIdentity(X509Certificate2 certificate, string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        string target = host.Trim().TrimEnd('.');

        foreach (X509Extension extension in certificate.Extensions)
        {
            if (extension is not X509SubjectAlternativeNameExtension san)
            {
                continue;
            }

            foreach (string dns in san.EnumerateDnsNames())
            {
                if (string.Equals(dns.TrimEnd('.'), target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (IPAddress ip in san.EnumerateIPAddresses())
            {
                if (IPAddress.TryParse(target, out IPAddress? parsed) && ip.Equals(parsed))
                {
                    return true;
                }
            }
        }

        // Fall back to CN only when SAN is absent (should be rare for RouterOS).
        string cn = certificate.GetNameInfo(X509NameType.DnsName, forIssuer: false);
        return !string.IsNullOrWhiteSpace(cn)
               && string.Equals(cn.TrimEnd('.'), target, StringComparison.OrdinalIgnoreCase);
    }
}
