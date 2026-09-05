using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Controller.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>W7-06: map mTLS client certificate to HttpContext.User principal.</summary>
public sealed class MtlsHttpContextUserW706LivingSpecTests
{
    [Fact]
    public void Ac1CreatesAuthenticatedPrincipalFromClientCertificateCn()
    {
        using X509Certificate2 cert = CreateSelfSigned("CN=desktop-lab-operator");
        ClaimsPrincipal? principal = MtlsClientCertificatePrincipalFactory.TryCreate(cert);

        Assert.NotNull(principal);
        Assert.True(principal!.Identity?.IsAuthenticated);
        Assert.Equal(MtlsClientCertificatePrincipalFactory.AuthenticationType, principal.Identity!.AuthenticationType);
        Assert.Equal("desktop-lab-operator", principal.Identity.Name);
        Assert.Equal(cert.Thumbprint, principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(cert.Thumbprint, principal.FindFirst("client_cert_thumbprint")?.Value);
    }

    [Fact]
    public void Ac1ReturnsNullWhenCertificateMissing()
    {
        Assert.Null(MtlsClientCertificatePrincipalFactory.TryCreate(null));
    }

    [Fact]
    public async Task Ac1MiddlewareSetsHttpContextUserFromClientCertificate()
    {
        using X509Certificate2 cert = CreateSelfSigned("CN=middleware-operator");
        bool nextCalled = false;
        MtlsClientCertificatePrincipalMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        DefaultHttpContext http = new();
        http.Connection.ClientCertificate = cert;

        await middleware.InvokeAsync(http);

        Assert.True(nextCalled);
        Assert.True(http.User.Identity?.IsAuthenticated);
        Assert.Equal("middleware-operator", http.User.Identity!.Name);
    }

    [Fact]
    public async Task Ac1MiddlewareDoesNotOverwriteExistingAuthenticatedUser()
    {
        using X509Certificate2 cert = CreateSelfSigned("CN=cert-operator");
        ClaimsIdentity existing = new(
            [new Claim(ClaimTypes.Name, "existing-operator")],
            authenticationType: "Test");
        DefaultHttpContext http = new()
        {
            User = new ClaimsPrincipal(existing),
        };
        http.Connection.ClientCertificate = cert;

        MtlsClientCertificatePrincipalMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(http);

        Assert.Equal("existing-operator", http.User.Identity!.Name);
    }

    private static X509Certificate2 CreateSelfSigned(string subject)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }
}
