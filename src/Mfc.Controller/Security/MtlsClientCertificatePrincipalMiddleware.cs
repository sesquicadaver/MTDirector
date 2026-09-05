using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace Mfc.Controller.Security;

/// <summary>
/// Sets <see cref="HttpContext.User"/> from the connection client certificate when not already authenticated (W7-06).
/// Relies on prior Kestrel <c>ClientCertificateValidation</c> (TrustedCa) for trust decisions.
/// </summary>
public sealed class MtlsClientCertificatePrincipalMiddleware
{
    private readonly RequestDelegate _next;

    public MtlsClientCertificatePrincipalMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            X509Certificate2? cert = context.Connection.ClientCertificate;
            ClaimsPrincipal? principal = MtlsClientCertificatePrincipalFactory.TryCreate(cert);
            if (principal is not null)
            {
                context.User = principal;
            }
        }

        return _next(context);
    }
}
