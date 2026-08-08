using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Secrets;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Secrets;

/// <summary>
/// Stores RouterOS connection profiles with envelope-encrypted passwords.
/// Desktop-facing views never include secret material.
/// </summary>
public sealed class ConnectionProfileService : IConnectionProfileService
{
    public const string SpkiPinChangedAction = "connection_profile.spki_pin_changed";
    public const string SecretRotatedAction = "connection_profile.secret_rotated";
    public const string UpsertedAction = "connection_profile.upserted";

    private readonly MfcDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly IAuditEventWriter _audit;

    public ConnectionProfileService(
        MfcDbContext db,
        ISecretProtector protector,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(audit);
        _db = db;
        _protector = protector;
        _audit = audit;
    }

    public async Task<ConnectionProfileView> UpsertAsync(
        UpsertConnectionProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureDeviceExists(command.DeviceId);

        ProtectedSecretMaterial protectedSecret = _protector.Protect(command.PasswordUtf8.Span);
        EncryptedSecretEntity secretEntity = new()
        {
            Id = Guid.NewGuid(),
            Ciphertext = protectedSecret.Ciphertext,
            WrappedDek = protectedSecret.WrappedDek,
            Algorithm = protectedSecret.Algorithm,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        DeviceConnectionProfile domain = DeviceConnectionProfile.Create(
            new DeviceId(command.DeviceId),
            NonEmptyName.Create(command.Username),
            SecretReference.From(secretEntity.Id),
            command.TrustMode,
            command.CaProfileRef,
            command.PinnedSpkiSha256,
            command.ConnectTimeoutMs,
            command.CommandTimeoutMs,
            command.MaxResponseBytes);

        DeviceConnectionProfileEntity? existing = await _db.DeviceConnectionProfiles
            .SingleOrDefaultAsync(p => p.DeviceId == command.DeviceId, cancellationToken)
            .ConfigureAwait(false);

        _db.EncryptedSecrets.Add(secretEntity);

        if (existing is null)
        {
            _db.DeviceConnectionProfiles.Add(ToEntity(domain));
        }
        else
        {
            ApplyDomain(existing, domain);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.AppendAsync(
            command.Actor,
            UpsertedAction,
            JsonSerializer.Serialize(new
            {
                device_id = command.DeviceId,
                trust_mode = command.TrustMode.ToString(),
                envelope_id = secretEntity.Id,
            }),
            cancellationToken).ConfigureAwait(false);

        return ToView(domain);
    }

    public async Task<ConnectionProfileView> RotatePasswordAsync(
        Guid deviceId,
        ReadOnlyMemory<byte> newPasswordUtf8,
        string actor,
        CancellationToken cancellationToken = default)
    {
        DeviceConnectionProfileEntity entity = await RequireProfileAsync(deviceId, cancellationToken)
            .ConfigureAwait(false);

        ProtectedSecretMaterial protectedSecret = _protector.Protect(newPasswordUtf8.Span);
        EncryptedSecretEntity secretEntity = new()
        {
            Id = Guid.NewGuid(),
            Ciphertext = protectedSecret.Ciphertext,
            WrappedDek = protectedSecret.WrappedDek,
            Algorithm = protectedSecret.Algorithm,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            RotatedAtUtc = DateTimeOffset.UtcNow,
        };

        DeviceConnectionProfile domain = FromEntity(entity);
        domain.RotateSecret(SecretReference.From(secretEntity.Id));
        ApplyDomain(entity, domain);
        _db.EncryptedSecrets.Add(secretEntity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.AppendAsync(
            actor,
            SecretRotatedAction,
            JsonSerializer.Serialize(new { device_id = deviceId, envelope_id = secretEntity.Id }),
            cancellationToken).ConfigureAwait(false);

        return ToView(domain);
    }

    public async Task<ConnectionProfileView> ChangeSpkiPinAsync(
        Guid deviceId,
        Hash256 newPin,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newPin);
        DeviceConnectionProfileEntity entity = await RequireProfileAsync(deviceId, cancellationToken)
            .ConfigureAwait(false);
        DeviceConnectionProfile domain = FromEntity(entity);
        string? previousPin = domain.PinnedSpkiSha256?.ToString();
        domain.ChangeSpkiPin(newPin);
        ApplyDomain(entity, domain);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.AppendAsync(
            actor,
            SpkiPinChangedAction,
            JsonSerializer.Serialize(new
            {
                device_id = deviceId,
                previous_pin = previousPin,
                new_pin = newPin.ToString(),
            }),
            cancellationToken).ConfigureAwait(false);

        return ToView(domain);
    }

    public async Task<ConnectionProfileView?> GetViewAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        DeviceConnectionProfileEntity? entity = await _db.DeviceConnectionProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.DeviceId == deviceId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToView(FromEntity(entity));
    }

    private void EnsureDeviceExists(Guid deviceId)
    {
        if (!_db.Devices.Any(d => d.Id == deviceId))
        {
            throw new InvalidOperationException($"Device '{deviceId}' does not exist.");
        }
    }

    private async Task<DeviceConnectionProfileEntity> RequireProfileAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        DeviceConnectionProfileEntity? entity = await _db.DeviceConnectionProfiles
            .SingleOrDefaultAsync(p => p.DeviceId == deviceId, cancellationToken)
            .ConfigureAwait(false);
        return entity
               ?? throw new InvalidOperationException($"Connection profile for device '{deviceId}' was not found.");
    }

    private static DeviceConnectionProfileEntity ToEntity(DeviceConnectionProfile domain) => new()
    {
        DeviceId = domain.DeviceId.Value,
        Username = domain.Username.Value,
        EncryptedSecretId = domain.SecretReference.Value,
        TrustMode = (short)domain.TrustMode,
        CaProfileRef = domain.CaProfileRef,
        PinnedSpkiSha256 = domain.PinnedSpkiSha256?.Bytes.ToArray(),
        ConnectTimeoutMs = domain.ConnectTimeoutMs,
        CommandTimeoutMs = domain.CommandTimeoutMs,
        MaxResponseBytes = domain.MaxResponseBytes,
        RowVersion = (long)domain.RowVersion,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static void ApplyDomain(DeviceConnectionProfileEntity entity, DeviceConnectionProfile domain)
    {
        entity.Username = domain.Username.Value;
        entity.EncryptedSecretId = domain.SecretReference.Value;
        entity.TrustMode = (short)domain.TrustMode;
        entity.CaProfileRef = domain.CaProfileRef;
        entity.PinnedSpkiSha256 = domain.PinnedSpkiSha256?.Bytes.ToArray();
        entity.ConnectTimeoutMs = domain.ConnectTimeoutMs;
        entity.CommandTimeoutMs = domain.CommandTimeoutMs;
        entity.MaxResponseBytes = domain.MaxResponseBytes;
        entity.RowVersion = (long)domain.RowVersion;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static DeviceConnectionProfile FromEntity(DeviceConnectionProfileEntity entity)
    {
        Hash256? pin = entity.PinnedSpkiSha256 is null ? null : Hash256.Create(entity.PinnedSpkiSha256);
        return DeviceConnectionProfile.Reconstitute(
            new DeviceId(entity.DeviceId),
            NonEmptyName.Create(entity.Username),
            SecretReference.From(entity.EncryptedSecretId),
            (CertificateTrustMode)entity.TrustMode,
            entity.CaProfileRef,
            pin,
            entity.ConnectTimeoutMs,
            entity.CommandTimeoutMs,
            entity.MaxResponseBytes,
            (ulong)entity.RowVersion);
    }

    private static ConnectionProfileView ToView(DeviceConnectionProfile domain) => new()
    {
        DeviceId = domain.DeviceId.Value,
        Username = domain.Username.Value,
        SecretReference = domain.SecretReference.Value,
        TrustMode = domain.TrustMode,
        CaProfileRef = domain.CaProfileRef,
        PinnedSpkiSha256Hex = domain.PinnedSpkiSha256?.ToString(),
        ConnectTimeoutMs = domain.ConnectTimeoutMs,
        CommandTimeoutMs = domain.CommandTimeoutMs,
        MaxResponseBytes = domain.MaxResponseBytes,
        RowVersion = domain.RowVersion,
    };
}
