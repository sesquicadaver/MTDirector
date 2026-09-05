using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Controller.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>W7-10: redacted CN + thumbprint prefix when mapping mTLS principal.</summary>
public sealed class MtlsPrincipalRedactedLogW710LivingSpecTests
{
    [Fact]
    public void Ac1FormatRedactedUsesThumbprintPrefixOnly()
    {
        string formatted = MtlsClientCertificateIdentityLog.FormatRedacted(
            "desktop-lab-operator",
            "ABCDEF0123456789DEADBEEF");

        Assert.Equal("cn=desktop-lab-operator; thumbprint=ABCDEF01…", formatted);
        Assert.DoesNotContain("DEADBEEF", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN CERTIFICATE", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ac1MiddlewareLogsRedactedIdentityOnPrincipalMap()
    {
        using X509Certificate2 cert = CreateSelfSigned("CN=log-operator");
        TestLogger logger = new();
        MtlsClientCertificatePrincipalMiddleware middleware = new(
            _ => Task.CompletedTask,
            logger);

        DefaultHttpContext http = new();
        http.Connection.ClientCertificate = cert;

        await middleware.InvokeAsync(http);

        Assert.True(http.User.Identity?.IsAuthenticated);
        Assert.Single(logger.InformationMessages);
        string message = logger.InformationMessages[0];
        Assert.Contains("Mapped mTLS client certificate", message, StringComparison.Ordinal);
        Assert.Contains("cn=log-operator", message, StringComparison.Ordinal);
        Assert.Contains("thumbprint=", message, StringComparison.Ordinal);
        Assert.Contains("…", message, StringComparison.Ordinal);
        Assert.DoesNotContain(cert.Thumbprint, message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ac1MiddlewareDoesNotLogWhenUserAlreadyAuthenticated()
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

        TestLogger logger = new();
        MtlsClientCertificatePrincipalMiddleware middleware = new(_ => Task.CompletedTask, logger);
        await middleware.InvokeAsync(http);

        Assert.Empty(logger.InformationMessages);
        Assert.Equal("existing-operator", http.User.Identity!.Name);
    }

    private static X509Certificate2 CreateSelfSigned(string subject)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    private sealed class TestLogger : ILogger<MtlsClientCertificatePrincipalMiddleware>
    {
        public List<string> InformationMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                InformationMessages.Add(formatter(state, exception));
            }
        }
    }
}
