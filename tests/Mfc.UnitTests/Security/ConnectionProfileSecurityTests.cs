using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Secrets;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence.Logging;
using Mfc.Infrastructure.Secrets;
using Mfc.Infrastructure.Security;
using Xunit;

namespace Mfc.UnitTests.Security;

public sealed class DeviceConnectionProfileTests
{
    [Fact]
    public void SpkiPinRequiresPinAndRejectsCaRef()
    {
        Assert.Throws<DomainInvariantException>(() =>
            DeviceConnectionProfile.Create(
                DeviceId.New(),
                NonEmptyName.Create("ro"),
                SecretReference.From(Guid.NewGuid()),
                CertificateTrustMode.SpkiPin,
                caProfileRef: "ca",
                pinnedSpkiSha256: Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray())));

        Assert.Throws<DomainInvariantException>(() =>
            DeviceConnectionProfile.Create(
                DeviceId.New(),
                NonEmptyName.Create("ro"),
                SecretReference.From(Guid.NewGuid()),
                CertificateTrustMode.SpkiPin,
                caProfileRef: null,
                pinnedSpkiSha256: null));
    }

    [Fact]
    public void InternalCaRequiresCaRefAndRotationKeepsDeviceId()
    {
        DeviceId deviceId = DeviceId.New();
        DeviceConnectionProfile profile = DeviceConnectionProfile.Create(
            deviceId,
            NonEmptyName.Create("monitor"),
            SecretReference.From(Guid.NewGuid()),
            CertificateTrustMode.InternalCa,
            caProfileRef: "internal-root",
            pinnedSpkiSha256: null);

        SecretReference next = SecretReference.From(Guid.NewGuid());
        profile.RotateSecret(next);
        Assert.Equal(deviceId, profile.DeviceId);
        Assert.Equal(next, profile.SecretReference);
        Assert.Equal(2UL, profile.RowVersion);
    }

    [Fact]
    public void ChangeSpkiPinBumpsRowVersion()
    {
        Hash256 pin = Hash256.Create(Enumerable.Repeat((byte)9, 32).ToArray());
        DeviceConnectionProfile profile = DeviceConnectionProfile.Create(
            DeviceId.New(),
            NonEmptyName.Create("ro"),
            SecretReference.From(Guid.NewGuid()),
            CertificateTrustMode.SpkiPin,
            caProfileRef: null,
            pinnedSpkiSha256: pin);

        Hash256 next = Hash256.Create(Enumerable.Repeat((byte)8, 32).ToArray());
        profile.ChangeSpkiPin(next);
        Assert.Equal(next, profile.PinnedSpkiSha256);
        Assert.Equal(2UL, profile.RowVersion);
    }
}

public sealed class SecretProtectorTests
{
    [Fact]
    public void ProtectRoundTripsAndLeaseClearsMemory()
    {
        DevelopmentMasterKeyProvider master = new();
        AesGcmSecretProtector protector = new(master);
        byte[] password = Encoding.UTF8.GetBytes("s3cret-value");

        ProtectedSecretMaterial material = protector.Protect(password);
        Assert.Equal(ProtectedSecretMaterial.Aes256GcmAlgorithm, material.Algorithm);
        Assert.DoesNotContain(password, material.Ciphertext);

        SecretLease lease = protector.Unprotect(material);
        Assert.True(lease.Plaintext.SequenceEqual(password));
        lease.Dispose();
        lease.Dispose(); // idempotent
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Plaintext.Length);
        Assert.Throws<ArgumentException>(() => protector.Protect(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void SecretLeaseRejectsNullAndProtectedMaterialHoldsAlgorithmConstant()
    {
        Assert.Throws<ArgumentNullException>(() => new SecretLease(null!));
        Assert.Equal("AES-256-GCM", ProtectedSecretMaterial.Aes256GcmAlgorithm);
        UpsertConnectionProfileCommand cmd = new()
        {
            DeviceId = Guid.NewGuid(),
            Username = "ro",
            PasswordUtf8 = Encoding.UTF8.GetBytes("x"),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "ca",
            Actor = "a",
        };
        Assert.Equal(DeviceConnectionProfile.MinConnectTimeoutMs * 5, cmd.ConnectTimeoutMs);
        Assert.Equal(30_000, cmd.CommandTimeoutMs);
        Assert.Equal(16_777_216, cmd.MaxResponseBytes);
    }
}

public sealed class ConnectionProfileViewAndRedactionTests
{
    [Fact]
    public void ConnectionProfileViewHasNoPasswordMembers()
    {
        PropertyInfo[] props = typeof(ConnectionProfileView).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.DoesNotContain(
            props,
            p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                 || p.Name.Contains("SecretText", StringComparison.OrdinalIgnoreCase)
                 || p.Name.Contains("Plain", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(props, p => p.Name == nameof(ConnectionProfileView.SecretReference));

        ConnectionProfileView view = new()
        {
            DeviceId = Guid.NewGuid(),
            Username = "ro",
            SecretReference = Guid.NewGuid(),
            TrustMode = CertificateTrustMode.SpkiPin,
            CaProfileRef = null,
            PinnedSpkiSha256Hex = new string('a', 64),
            ConnectTimeoutMs = 5000,
            CommandTimeoutMs = 30000,
            MaxResponseBytes = 16_777_216,
            RowVersion = 1,
        };
        Assert.Equal("ro", view.Username);
        Assert.Equal(CertificateTrustMode.SpkiPin, view.TrustMode);
        Assert.Null(view.CaProfileRef);
        Assert.Equal(64, view.PinnedSpkiSha256Hex!.Length);
        Assert.Equal(5000, view.ConnectTimeoutMs);
        Assert.Equal(30000, view.CommandTimeoutMs);
        Assert.Equal(16_777_216, view.MaxResponseBytes);
        Assert.Equal(1UL, view.RowVersion);
        Assert.NotEqual(Guid.Empty, view.DeviceId);
        Assert.NotEqual(Guid.Empty, view.SecretReference);
    }

    [Fact]
    public void RedactionRemovesPasswordEqualsAndJsonPassword()
    {
        string redacted = RedactingJsonConsoleLoggerProvider.RedactForTests(
            """Password=super-secret; {"password":"super-secret"} """);
        Assert.DoesNotContain("super-secret", redacted, StringComparison.Ordinal);
        Assert.Contains("Password=***", redacted, StringComparison.OrdinalIgnoreCase);
    }
}
