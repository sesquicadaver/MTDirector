namespace Mfc.RouterOs.Transport;

/// <summary>TLS / authentication fault codes (Read Adapter Spec §14–15).</summary>
public static class ApiSslErrors
{
    public const string CertificateMismatch = "TLS_CERTIFICATE_MISMATCH";

    public const string CertificateExpired = "TLS_CERTIFICATE_EXPIRED";

    public const string HostnameMismatch = "TLS_HOSTNAME_MISMATCH";

    public const string HandshakeFailed = "TLS_HANDSHAKE_FAILED";

    public const string UnsupportedLegacyAuth = "UNSUPPORTED_LEGACY_AUTH_FLOW";

    public const string AuthenticationFailed = "API_AUTHENTICATION_FAILED";

    public const string AuthenticationTimeout = "API_AUTHENTICATION_TIMEOUT";

    public const string PlainApiForbidden = "TLS_PLAIN_API_FORBIDDEN";
}

/// <summary>Exception for API-SSL connect/login failures. Never embeds password material.</summary>
public sealed class ApiSslException : Exception
{
    public ApiSslException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public ApiSslException(string code, string message, Exception inner)
        : base(message, inner)
    {
        Code = code;
    }

    public string Code { get; }
}
