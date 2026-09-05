using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Controller.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>W7-13: mTLS principal-map log includes HttpContext.TraceIdentifier.</summary>
public sealed class MtlsPrincipalMapTraceIdW713LivingSpecTests
{
    [Fact]
    public async Task Ac1MiddlewareLogIncludesTraceIdentifier()
    {
        using X509Certificate2 cert = CreateSelfSigned("CN=trace-operator");
        TestLogger logger = new();
        MtlsClientCertificatePrincipalMiddleware middleware = new(_ => Task.CompletedTask, logger);

        DefaultHttpContext http = new();
        http.TraceIdentifier = "00-w713-correlation-id-demo";
        http.Connection.ClientCertificate = cert;

        await middleware.InvokeAsync(http);

        Assert.Single(logger.InformationMessages);
        string message = logger.InformationMessages[0];
        Assert.Contains("TraceIdentifier=00-w713-correlation-id-demo", message, StringComparison.Ordinal);
        Assert.Contains("cn=trace-operator", message, StringComparison.Ordinal);
        Assert.DoesNotContain(cert.Thumbprint, message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN CERTIFICATE", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ac1MiddlewareStillOmitsFullThumbprintWithTraceId()
    {
        using X509Certificate2 cert = CreateSelfSigned("CN=safe-operator");
        TestLogger logger = new();
        MtlsClientCertificatePrincipalMiddleware middleware = new(_ => Task.CompletedTask, logger);

        DefaultHttpContext http = new();
        http.TraceIdentifier = "trace-abc";
        http.Connection.ClientCertificate = cert;

        await middleware.InvokeAsync(http);

        string message = Assert.Single(logger.InformationMessages);
        Assert.Contains("thumbprint=", message, StringComparison.Ordinal);
        Assert.Contains("…", message, StringComparison.Ordinal);
        Assert.DoesNotContain(cert.Thumbprint, message, StringComparison.OrdinalIgnoreCase);
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
