using System.Security.Cryptography.X509Certificates;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Abstractions.Secrets;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mfc.Infrastructure.RouterOs;

/// <summary>Loads username, decrypted password, and trust metadata for RouterOS read targets.</summary>
public sealed class EfRouterOsConnectionMaterializer : IRouterOsConnectionMaterializer
{
    private readonly MfcDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly IRouterOsTrustedCaStore _trustedCaStore;
    private readonly TrustedCaStoreOptions _trustedCaOptions;

    public EfRouterOsConnectionMaterializer(
        MfcDbContext db,
        ISecretProtector protector,
        IRouterOsTrustedCaStore trustedCaStore,
        IOptions<TrustedCaStoreOptions> trustedCaOptions)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(trustedCaStore);
        ArgumentNullException.ThrowIfNull(trustedCaOptions);
        _db = db;
        _protector = protector;
        _trustedCaStore = trustedCaStore;
        _trustedCaOptions = trustedCaOptions.Value ?? new TrustedCaStoreOptions();
    }

    public async Task<RouterOsConnectionMaterial> MaterializeAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        DeviceConnectionProfileEntity? profile = await _db.DeviceConnectionProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.DeviceId == target.DeviceId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            throw new InvalidOperationException(
                $"Connection profile for device '{target.DeviceId}' is missing.");
        }

        if (profile.EncryptedSecretId != target.SecretReference.Value)
        {
            throw new InvalidOperationException(
                "Connection profile secret reference does not match the RouterOS read target.");
        }

        EncryptedSecretEntity? secretEntity = await _db.EncryptedSecrets.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == profile.EncryptedSecretId, cancellationToken)
            .ConfigureAwait(false);
        if (secretEntity is null)
        {
            throw new InvalidOperationException(
                $"Encrypted secret '{profile.EncryptedSecretId}' for device '{target.DeviceId}' was not found.");
        }

        SecretLease password = _protector.Unprotect(new ProtectedSecretMaterial
        {
            Ciphertext = secretEntity.Ciphertext,
            WrappedDek = secretEntity.WrappedDek,
            Algorithm = secretEntity.Algorithm,
        });

        CertificateTrustMode trustMode = (CertificateTrustMode)profile.TrustMode;
        Hash256? pin = profile.PinnedSpkiSha256 is null ? null : Hash256.Create(profile.PinnedSpkiSha256);
        IReadOnlyList<byte[]> trustedCa = [];
        X509RevocationMode revocationMode = X509RevocationMode.NoCheck;
        if (trustMode == CertificateTrustMode.InternalCa)
        {
            if (string.IsNullOrWhiteSpace(profile.CaProfileRef))
            {
                password.Dispose();
                throw new InvalidOperationException(
                    "INTERNAL_CA trust requires CaProfileRef on the connection profile.");
            }

            trustedCa = _trustedCaStore.GetCertificateDerBytes(profile.CaProfileRef);
            if (trustedCa.Count == 0)
            {
                password.Dispose();
                throw new InvalidOperationException(
                    $"Trusted CA profile '{profile.CaProfileRef}' is not configured on the Controller.");
            }

            try
            {
                revocationMode = TrustedCaRevocationModes.Parse(_trustedCaOptions.RevocationMode);
            }
            catch
            {
                password.Dispose();
                throw;
            }
        }

        return new RouterOsConnectionMaterial
        {
            Host = target.Endpoint.Host.Value,
            Port = target.Endpoint.Port,
            Username = profile.Username,
            Password = password,
            TrustMode = trustMode,
            PinnedSpkiSha256 = pin,
            TrustedCaCertificatesDer = trustedCa,
            CertificateRevocationMode = revocationMode,
            ConnectTimeoutMs = profile.ConnectTimeoutMs,
            CommandTimeoutMs = profile.CommandTimeoutMs,
        };
    }
}
