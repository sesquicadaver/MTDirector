using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Mfc.Controller.Configuration;

/// <summary>Parses <see cref="GrpcHostOptions.ClientCertificateMode"/> for Kestrel HTTPS (W7-03).</summary>
public static class GrpcClientCertificateModeParser
{
    public const string NoCertificate = "NoCertificate";
    public const string AllowCertificate = "AllowCertificate";
    public const string RequireCertificate = "RequireCertificate";

    /// <summary>Maps configured mode string to Kestrel <see cref="ClientCertificateMode"/>.</summary>
    public static ClientCertificateMode Parse(string? mode)
    {
        string trimmed = mode?.Trim() ?? string.Empty;
        if (trimmed.Length == 0
            || string.Equals(trimmed, NoCertificate, StringComparison.OrdinalIgnoreCase))
        {
            return ClientCertificateMode.NoCertificate;
        }

        if (string.Equals(trimmed, AllowCertificate, StringComparison.OrdinalIgnoreCase))
        {
            return ClientCertificateMode.AllowCertificate;
        }

        if (string.Equals(trimmed, RequireCertificate, StringComparison.OrdinalIgnoreCase))
        {
            return ClientCertificateMode.RequireCertificate;
        }

        throw new InvalidOperationException(
            $"Unknown Mfc:Grpc:ClientCertificateMode '{mode}'. Supported: {NoCertificate}, {AllowCertificate}, {RequireCertificate}.");
    }

    /// <summary>True when the mode requests or allows a client certificate.</summary>
    public static bool RequestsOrAllowsClientCertificate(ClientCertificateMode mode)
        => mode is ClientCertificateMode.AllowCertificate or ClientCertificateMode.RequireCertificate;
}
