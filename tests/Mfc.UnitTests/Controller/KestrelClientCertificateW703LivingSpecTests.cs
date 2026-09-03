using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Controller.Configuration;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>W7-03: Kestrel ClientCertificateMode + Desktop client cert presentation.</summary>
public sealed class KestrelClientCertificateW703LivingSpecTests
{
    [Theory]
    [InlineData(null, ClientCertificateMode.NoCertificate)]
    [InlineData("", ClientCertificateMode.NoCertificate)]
    [InlineData("NoCertificate", ClientCertificateMode.NoCertificate)]
    [InlineData("AllowCertificate", ClientCertificateMode.AllowCertificate)]
    [InlineData("RequireCertificate", ClientCertificateMode.RequireCertificate)]
    [InlineData("allowcertificate", ClientCertificateMode.AllowCertificate)]
    public void Ac1ParsesClientCertificateMode(string? input, ClientCertificateMode expected)
    {
        Assert.Equal(expected, GrpcClientCertificateModeParser.Parse(input));
    }

    [Fact]
    public void Ac1RejectsUnknownClientCertificateMode()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => GrpcClientCertificateModeParser.Parse("Maybe"));
        Assert.Contains("ClientCertificateMode", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac1HttpBindRejectsNonNoCertificateMode()
    {
        ControllerOptions options = new()
        {
            Grpc = new GrpcHostOptions
            {
                ListenAddress = "http://127.0.0.1:5101",
                AllowInsecureLoopback = true,
                ClientCertificateMode = GrpcClientCertificateModeParser.RequireCertificate,
            },
            Security = new SecurityHostOptions { MasterKeyProvider = "Development" },
            Authentication = new AuthenticationHostOptions
            {
                AllowDevelopmentAuthentication = true,
                AllowMetadataActor = true,
            },
            Database = new DatabaseHostOptions
            {
                ConnectionString = "Host=127.0.0.1;Database=mfc;Username=mfc;Password=secret",
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ControllerOptionsValidator.Validate(options, Environments.Development));
        Assert.Contains("https://", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac2DesktopHandlerAttachesConfiguredClientCertificate()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mfc-w703-{Guid.NewGuid():N}.pfx");
        try
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new(
                "CN=desktop-lab-operator",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using X509Certificate2 cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(30));
            // Passwordless test PFX — avoids secret-scanner false positives on fixture strings.
            byte[] pfx = cert.Export(X509ContentType.Pfx);
            File.WriteAllBytes(path, pfx);

            DesktopOptions options = new()
            {
                ControllerEndpoint = "https://127.0.0.1:5101",
                HealthCheckTimeoutSeconds = 5,
                ClientCertificatePath = path,
            };

            using SocketsHttpHandler handler = DesktopGrpcHttpHandlerFactory.Create(options);
            Assert.NotNull(handler.SslOptions.ClientCertificates);
            Assert.Single(handler.SslOptions.ClientCertificates!);
            Assert.Equal("CN=desktop-lab-operator", handler.SslOptions.ClientCertificates[0].Subject);
            Assert.NotNull(handler.SslOptions.LocalCertificateSelectionCallback);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Ac2DesktopHandlerOmitsClientCertificateWhenPathEmpty()
    {
        DesktopOptions options = new()
        {
            ControllerEndpoint = "http://127.0.0.1:5101",
            ClientCertificatePath = "",
        };

        using SocketsHttpHandler handler = DesktopGrpcHttpHandlerFactory.Create(options);
        Assert.True(
            handler.SslOptions.ClientCertificates is null
            || handler.SslOptions.ClientCertificates.Count == 0);
        Assert.Null(handler.SslOptions.LocalCertificateSelectionCallback);
    }

    [Fact]
    public void Ac2MissingClientCertificateFileFailsClosed()
    {
        DesktopOptions options = new()
        {
            ClientCertificatePath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pfx"),
        };

        Assert.Throws<FileNotFoundException>(() => DesktopGrpcHttpHandlerFactory.Create(options));
    }
}
