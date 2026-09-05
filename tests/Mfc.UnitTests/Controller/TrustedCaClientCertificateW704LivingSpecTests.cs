using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Controller.Configuration;
using Mfc.Controller.Security;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>W7-04: inbound mTLS client cert validation against TrustedCa.</summary>
public sealed class TrustedCaClientCertificateW704LivingSpecTests
{
    [Fact]
    public void Ac1TrustedClientCertificateIsAccepted()
    {
        using RSA caKey = RSA.Create(2048);
        using X509Certificate2 ca = CreateSelfSignedCa(caKey, "CN=mfc-lab-ca");
        using X509Certificate2 client = CreateClientCert(ca, "CN=desktop-operator");

        bool ok = TrustedCaClientCertificateValidator.Validate(
            client,
            chain: null,
            SslPolicyErrors.RemoteCertificateChainErrors,
            ClientCertificateMode.RequireCertificate,
            [Clone(ca)],
            X509RevocationMode.NoCheck);

        Assert.True(ok);
    }

    [Fact]
    public void Ac1UntrustedClientCertificateIsRejected()
    {
        using RSA caKey = RSA.Create(2048);
        using X509Certificate2 ca = CreateSelfSignedCa(caKey, "CN=mfc-lab-ca");
        using RSA otherKey = RSA.Create(2048);
        using X509Certificate2 otherCa = CreateSelfSignedCa(otherKey, "CN=other-ca");
        using X509Certificate2 client = CreateClientCert(otherCa, "CN=spoof");

        bool ok = TrustedCaClientCertificateValidator.Validate(
            client,
            chain: null,
            SslPolicyErrors.None,
            ClientCertificateMode.RequireCertificate,
            [Clone(ca)],
            X509RevocationMode.NoCheck);

        Assert.False(ok);
    }

    [Fact]
    public void Ac1MissingTrustedRootsFailClosed()
    {
        using RSA caKey = RSA.Create(2048);
        using X509Certificate2 ca = CreateSelfSignedCa(caKey, "CN=mfc-lab-ca");
        using X509Certificate2 client = CreateClientCert(ca, "CN=desktop-operator");

        bool ok = TrustedCaClientCertificateValidator.Validate(
            client,
            chain: null,
            SslPolicyErrors.None,
            ClientCertificateMode.RequireCertificate,
            [],
            X509RevocationMode.NoCheck);

        Assert.False(ok);
    }

    [Fact]
    public void Ac1AllowModeAcceptsNullCertificate()
    {
        Assert.True(TrustedCaClientCertificateValidator.Validate(
            certificate: null,
            chain: null,
            SslPolicyErrors.None,
            ClientCertificateMode.AllowCertificate,
            [],
            X509RevocationMode.NoCheck));
    }

    [Fact]
    public void Ac1RequireModeRejectsNullCertificate()
    {
        Assert.False(TrustedCaClientCertificateValidator.Validate(
            certificate: null,
            chain: null,
            SslPolicyErrors.None,
            ClientCertificateMode.RequireCertificate,
            [],
            X509RevocationMode.NoCheck));
    }

    [Fact]
    public void Ac1AllowOrRequireWithoutClientCaProfileRefIsRejected()
    {
        ControllerOptions options = new()
        {
            Grpc = new GrpcHostOptions
            {
                ListenAddress = "https://127.0.0.1:5101",
                ClientCertificateMode = GrpcClientCertificateModeParser.RequireCertificate,
            },
            Security = new SecurityHostOptions
            {
                RequireTls = true,
                MasterKeyProvider = "OsKeyStore",
                TrustedCa = new TrustedCaHostOptions
                {
                    ProfilesDirectory = "/var/lib/mfc/trusted-ca",
                    RevocationMode = "Online",
                    ClientCaProfileRef = "",
                },
            },
            Authentication = new AuthenticationHostOptions(),
            Database = new DatabaseHostOptions
            {
                ConnectionString = "Host=127.0.0.1;Database=mfc;Username=mfc;Password=secret",
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ControllerOptionsValidator.Validate(options, Environments.Production));
        Assert.Contains("ClientCaProfileRef", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac1AllowOrRequireWithoutProfilesDirectoryIsRejected()
    {
        ControllerOptions options = new()
        {
            Grpc = new GrpcHostOptions
            {
                ListenAddress = "https://127.0.0.1:5101",
                ClientCertificateMode = GrpcClientCertificateModeParser.AllowCertificate,
            },
            Security = new SecurityHostOptions
            {
                RequireTls = true,
                MasterKeyProvider = "OsKeyStore",
                TrustedCa = new TrustedCaHostOptions
                {
                    ProfilesDirectory = "",
                    RevocationMode = "Online",
                    ClientCaProfileRef = "desktop-mtls",
                },
            },
            Authentication = new AuthenticationHostOptions(),
            Database = new DatabaseHostOptions
            {
                ConnectionString = "Host=127.0.0.1;Database=mfc;Username=mfc;Password=secret",
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ControllerOptionsValidator.Validate(options, Environments.Production));
        Assert.Contains("ProfilesDirectory", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac1LoadTrustedRootsRoundTripsDer()
    {
        using RSA caKey = RSA.Create(2048);
        using X509Certificate2 ca = CreateSelfSignedCa(caKey, "CN=mfc-lab-ca");
        byte[] der = ca.Export(X509ContentType.Cert);

        IReadOnlyList<X509Certificate2> roots = TrustedCaClientCertificateValidator.LoadTrustedRoots([der]);
        Assert.Single(roots);
        Assert.Equal(ca.Thumbprint, roots[0].Thumbprint);
        roots[0].Dispose();
    }

    private static X509Certificate2 CreateSelfSignedCa(RSA key, string subject)
    {
        CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreateClientCert(X509Certificate2 issuer, string subject)
    {
        using RSA clientKey = RSA.Create(2048);
        CertificateRequest request = new(subject, clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") },
                critical: true));

        byte[] serial = new byte[8];
        RandomNumberGenerator.Fill(serial);
        using X509Certificate2 cert = request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(14),
            serial);
        return cert.CopyWithPrivateKey(clientKey);
    }

    private static X509Certificate2 Clone(X509Certificate2 cert)
        => X509CertificateLoader.LoadCertificate(cert.Export(X509ContentType.Cert));
}
