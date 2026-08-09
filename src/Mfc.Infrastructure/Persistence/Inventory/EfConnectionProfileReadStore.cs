using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Inventory;

/// <summary>
/// Reads connection profile fields for RouterOS targeting without exposing password ciphertext.
/// </summary>
public sealed class EfConnectionProfileReadStore : IConnectionProfileReadStore
{
    private readonly MfcDbContext _db;

    public EfConnectionProfileReadStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<ConnectionProfileReadModel?> GetAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        DeviceConnectionProfileEntity? entity = await _db.DeviceConnectionProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.DeviceId == deviceId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        Hash256? pin = entity.PinnedSpkiSha256 is null ? null : Hash256.Create(entity.PinnedSpkiSha256);
        return new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(entity.EncryptedSecretId),
            TrustMode = (CertificateTrustMode)entity.TrustMode,
            CaProfileRef = entity.CaProfileRef,
            PinnedSpkiSha256 = pin,
        };
    }
}
