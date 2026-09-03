using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Inventory;
using Mfc.Infrastructure.RouterOs;
using Mfc.Infrastructure.Security;
using Mfc.RouterOs.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-04 (#377) — directory CA store + INTERNAL_CA revocation policy.</summary>
public sealed class TrustedCaStoreSec04LivingSpecTests
{
    [Fact]
    public void Ac1DirectoryStoreLoadsPemForProfileRef()
    {
        string root = CreateTempProfilesRoot();
        try
        {
            using X509Certificate2 ca = CreateSelfSignedCa("CN=SEC-04 Lab CA");
            string profileDir = Path.Combine(root, "lab-ca");
            Directory.CreateDirectory(profileDir);
            File.WriteAllBytes(Path.Combine(profileDir, "root.pem"), ca.Export(X509ContentType.Cert));

            DirectoryRouterOsTrustedCaStore store = new(new TrustedCaStoreOptions
            {
                ProfilesDirectory = root,
                RevocationMode = "Online",
            });

            IReadOnlyList<byte[]> der = store.GetCertificateDerBytes("lab-ca");
            Assert.Single(der);
            using X509Certificate2 loaded = X509CertificateLoader.LoadCertificate(der[0]);
            Assert.Equal(ca.Thumbprint, loaded.Thumbprint);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Ac2MissingProfileOrDirectoryIsEmptyFailClosedMaterial()
    {
        DirectoryRouterOsTrustedCaStore emptyDir = new(new TrustedCaStoreOptions());
        Assert.Empty(emptyDir.GetCertificateDerBytes("any"));

        string root = CreateTempProfilesRoot();
        try
        {
            DirectoryRouterOsTrustedCaStore store = new(new TrustedCaStoreOptions
            {
                ProfilesDirectory = root,
            });
            Assert.Empty(store.GetCertificateDerBytes("missing-profile"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Ac3PathTraversalCaProfileRefIsRejected()
    {
        string root = CreateTempProfilesRoot();
        try
        {
            DirectoryRouterOsTrustedCaStore store = new(new TrustedCaStoreOptions
            {
                ProfilesDirectory = root,
            });
            Assert.Throws<InvalidOperationException>(() => store.GetCertificateDerBytes("../etc"));
            Assert.Throws<InvalidOperationException>(() => store.GetCertificateDerBytes("a/b"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Ac4InternalCaRevocationModeIsAppliedNotHardcodedNoCheck()
    {
        using X509Certificate2 ca = CreateSelfSignedCa("CN=SEC-04 Revocation CA");
        using X509Certificate2 server = CreateServerCert(ca, "127.0.0.1");
        X509Certificate2Collection roots = [ca];

        using SecretLease password = new("x"u8);
        ApiSslConnectOptions online = new()
        {
            Host = "127.0.0.1",
            Username = "ro",
            Password = password,
            TrustMode = CertificateTrustMode.InternalCa,
            TrustedRootCertificates = roots,
            CertificateRevocationMode = X509RevocationMode.Online,
        };

        // Lab self-signed chain without CRL/OCSP distribution points fails Online revocation.
        Assert.False(ApiSslCertificateValidator.Validate(
            server,
            chain: null,
            SslPolicyErrors.RemoteCertificateChainErrors,
            online,
            out ApiSslException? onlineError));
        Assert.Equal(ApiSslErrors.CertificateMismatch, onlineError!.Code);

        ApiSslConnectOptions noCheck = new()
        {
            Host = "127.0.0.1",
            Username = "ro",
            Password = password,
            TrustMode = CertificateTrustMode.InternalCa,
            TrustedRootCertificates = roots,
            CertificateRevocationMode = X509RevocationMode.NoCheck,
        };
        Assert.True(ApiSslCertificateValidator.Validate(
            server,
            chain: null,
            SslPolicyErrors.None,
            noCheck,
            out ApiSslException? noCheckError));
        Assert.Null(noCheckError);
    }

    [Fact]
    public void Ac5ProductionDiRegistersDirectoryStoreNotNotConfigured()
    {
        ServiceCollection services = new();
        services.AddOptions<TrustedCaStoreOptions>();
        services.AddMfcSecrets("Development");
        using ServiceProvider sp = services.BuildServiceProvider();
        IRouterOsTrustedCaStore store = sp.GetRequiredService<IRouterOsTrustedCaStore>();
        Assert.IsType<DirectoryRouterOsTrustedCaStore>(store);
        Assert.IsNotType<NotConfiguredRouterOsTrustedCaStore>(store);
    }

    [Fact]
    public void Ac6RevocationModeParserDefaultsToOnlineAndRejectsUnknown()
    {
        Assert.Equal(X509RevocationMode.Online, TrustedCaRevocationModes.Parse(null));
        Assert.Equal(X509RevocationMode.Online, TrustedCaRevocationModes.Parse("Online"));
        Assert.Equal(X509RevocationMode.Offline, TrustedCaRevocationModes.Parse("Offline"));
        Assert.Equal(X509RevocationMode.NoCheck, TrustedCaRevocationModes.Parse("NoCheck"));
        Assert.Throws<InvalidOperationException>(() => TrustedCaRevocationModes.Parse("Skip"));
    }

    [Fact]
    public void Ac7OptionsBinderMapsControllerTrustedCaSection()
    {
        TrustedCaStoreOptions options = new()
        {
            ProfilesDirectory = "/var/lib/mfc/trusted-ca",
            RevocationMode = "Offline",
        };
        IOptions<TrustedCaStoreOptions> wrapped = Options.Create(options);
        DirectoryRouterOsTrustedCaStore store = new(wrapped);
        Assert.Empty(store.GetCertificateDerBytes("profile"));
        Assert.Equal(X509RevocationMode.Offline, TrustedCaRevocationModes.Parse(options.RevocationMode));
    }

    private static string CreateTempProfilesRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "mfc-sec04-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static X509Certificate2 CreateSelfSignedCa(string subject)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static X509Certificate2 CreateServerCert(X509Certificate2 ca, string sanIp)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new($"CN={sanIp}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        SubjectAlternativeNameBuilder san = new();
        san.AddIpAddress(System.Net.IPAddress.Parse(sanIp));
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        byte[] serial = new byte[8];
        RandomNumberGenerator.Fill(serial);
        serial[0] &= 0x7F;
        return request.Create(ca, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30), serial)
            .CopyWithPrivateKey(key);
    }
}
