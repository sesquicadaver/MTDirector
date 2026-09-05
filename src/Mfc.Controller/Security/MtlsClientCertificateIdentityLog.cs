namespace Mfc.Controller.Security;

/// <summary>
/// Redacted identity fragments for mTLS principal-map logs (W7-10).
/// Never logs PEM, passwords, or full certificates.
/// </summary>
public static class MtlsClientCertificateIdentityLog
{
    /// <summary>Number of thumbprint hex characters kept in logs (prefix only).</summary>
    public const int ThumbprintPrefixLength = 8;

    /// <summary>
    /// Formats <c>cn=…; thumbprint=ABCD…</c> with a truncated thumbprint prefix.
    /// </summary>
    public static string FormatRedacted(string commonName, string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commonName);
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

        string cn = commonName.Trim();
        string thumb = thumbprint.Trim();
        string prefix = thumb.Length <= ThumbprintPrefixLength
            ? thumb
            : thumb[..ThumbprintPrefixLength];

        return $"cn={cn}; thumbprint={prefix}…";
    }
}
