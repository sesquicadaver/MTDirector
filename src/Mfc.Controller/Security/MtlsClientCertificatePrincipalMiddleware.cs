using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace Mfc.Controller.Security;

/// <summary>
/// Sets <see cref="HttpContext.User"/> from the connection client certificate when not already authenticated (W7-06).
/// Relies on prior Kestrel <c>ClientCertificateValidation</c> (TrustedCa) for trust decisions.
/// Logs a redacted CN + thumbprint prefix and request <see cref="HttpContext.TraceIdentifier"/> on successful map (W7-10 / W7-13).
/// </summary>
public sealed partial class MtlsClientCertificatePrincipalMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MtlsClientCertificatePrincipalMiddleware> _logger;

    public MtlsClientCertificatePrincipalMiddleware(
        RequestDelegate next,
        ILogger<MtlsClientCertificatePrincipalMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            X509Certificate2? cert = context.Connection.ClientCertificate;
            ClaimsPrincipal? principal = MtlsClientCertificatePrincipalFactory.TryCreate(cert);
            if (principal is not null && cert is not null)
            {
                context.User = principal;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    string cn = principal.Identity?.Name ?? string.Empty;
                    string identity = MtlsClientCertificateIdentityLog.FormatRedacted(cn, cert.Thumbprint);
                    LogMappedPrincipal(_logger, identity, context.TraceIdentifier);
                }
            }
        }

        return _next(context);
    }

    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Information,
        Message = "Mapped mTLS client certificate to HttpContext.User ({Identity}) TraceIdentifier={TraceIdentifier}")]
    private static partial void LogMappedPrincipal(ILogger logger, string identity, string traceIdentifier);
}
