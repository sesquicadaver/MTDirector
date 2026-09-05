using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W7-05: Desktop gRPC actor from client certificate CN.</summary>
public sealed class DesktopGrpcActorFromCertCnW705LivingSpecTests
{
    public DesktopGrpcActorFromCertCnW705LivingSpecTests()
    {
        DesktopGrpcActorResolver.ClearCache();
    }

    [Fact]
    public void Ac1UsesConfiguredActorWhenNoClientCertificate()
    {
        DesktopOptions options = new()
        {
            Actor = "operator@lab",
            ClientCertificatePath = "",
        };

        Assert.Equal("operator@lab", DesktopGrpcActorResolver.Resolve(options));
        Assert.Equal(
            "operator@lab",
            DesktopGrpcActorResolver.CreateHeaders(options).GetValue(DesktopGrpcActorResolver.MetadataKey));
    }

    [Fact]
    public void Ac1DefaultsToDesktopWhenActorMissingAndNoCert()
    {
        DesktopOptions options = new()
        {
            Actor = "  ",
            ClientCertificatePath = null,
        };

        Assert.Equal(DesktopGrpcActorResolver.DefaultActor, DesktopGrpcActorResolver.Resolve(options));
    }

    [Fact]
    public void Ac1PrefersClientCertificateCnOverConfiguredActor()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mfc-w705-{Guid.NewGuid():N}.pfx");
        try
        {
            WritePasswordlessClientPfx(path, "CN=desktop-lab-operator");

            DesktopOptions options = new()
            {
                Actor = "should-be-ignored",
                ClientCertificatePath = path,
            };

            Assert.Equal("desktop-lab-operator", DesktopGrpcActorResolver.Resolve(options));
            Assert.Equal(
                "desktop-lab-operator",
                DesktopGrpcActorResolver.CreateHeaders(options).GetValue(DesktopGrpcActorResolver.MetadataKey));
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
    public void Ac1MissingClientCertificateFileFailsClosed()
    {
        DesktopOptions options = new()
        {
            Actor = "desktop",
            ClientCertificatePath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pfx"),
        };

        Assert.Throws<FileNotFoundException>(() => DesktopGrpcActorResolver.Resolve(options));
    }

    private static void WritePasswordlessClientPfx(string path, string subject)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));
        File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx));
    }
}
