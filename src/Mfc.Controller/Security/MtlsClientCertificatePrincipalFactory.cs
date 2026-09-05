using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace Mfc.Controller.Security;

/// <summary>
/// Builds an authenticated <see cref="ClaimsPrincipal"/> from an inbound mTLS client certificate (W7-06).
/// Used after Kestrel TrustedCa validation so actor resolution can read <c>HttpContext.User</c>.
/// </summary>
public static class MtlsClientCertificatePrincipalFactory
{
    /// <summary>Authentication type stamped on the identity.</summary>
    public const string AuthenticationType = "Certificate";

    /// <summary>
    /// Creates a principal with <see cref="ClaimTypes.Name"/> = certificate CN when present; otherwise null.
    /// </summary>
    public static ClaimsPrincipal? TryCreate(X509Certificate2? certificate)
    {
        if (certificate is null)
        {
            return null;
        }

        string? cn = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        if (string.IsNullOrWhiteSpace(cn))
        {
            return null;
        }

        string name = cn.Trim();
        Claim[] claims =
        [
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.NameIdentifier, certificate.Thumbprint),
            new Claim("client_cert_thumbprint", certificate.Thumbprint),
        ];

        ClaimsIdentity identity = new(claims, AuthenticationType);
        return new ClaimsPrincipal(identity);
    }
}
