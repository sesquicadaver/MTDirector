using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Abstractions.ConnectionProfiles;

/// <summary>Desktop/gRPC-safe view of a connection profile — never includes password material.</summary>
public sealed class ConnectionProfileView
{
    public required Guid DeviceId { get; init; }

    public required string Username { get; init; }

    public required Guid SecretReference { get; init; }

    public required CertificateTrustMode TrustMode { get; init; }

    public string? CaProfileRef { get; init; }

    public string? PinnedSpkiSha256Hex { get; init; }

    public required int ConnectTimeoutMs { get; init; }

    public required int CommandTimeoutMs { get; init; }

    public required long MaxResponseBytes { get; init; }

    public required ulong RowVersion { get; init; }
}

public sealed class UpsertConnectionProfileCommand
{
    public required Guid DeviceId { get; init; }

    public required string Username { get; init; }

    public required ReadOnlyMemory<byte> PasswordUtf8 { get; init; }

    public required CertificateTrustMode TrustMode { get; init; }

    public string? CaProfileRef { get; init; }

    public Hash256? PinnedSpkiSha256 { get; init; }

    public int ConnectTimeoutMs { get; init; } = DeviceConnectionProfile.MinConnectTimeoutMs * 5;

    public int CommandTimeoutMs { get; init; } = 30_000;

    public long MaxResponseBytes { get; init; } = 16_777_216;

    public required string Actor { get; init; }

    /// <summary>Client-supplied idempotency key for UpdateDeviceConnection (M1-25).</summary>
    public Guid IdempotencyKey { get; init; }
}

/// <summary>Persists encrypted connection profiles without exposing secrets to Desktop clients.</summary>
public interface IConnectionProfileService
{
    Task<ConnectionProfileView> UpsertAsync(
        UpsertConnectionProfileCommand command,
        CancellationToken cancellationToken = default);

    Task<ConnectionProfileView> RotatePasswordAsync(
        Guid deviceId,
        ReadOnlyMemory<byte> newPasswordUtf8,
        string actor,
        CancellationToken cancellationToken = default);

    Task<ConnectionProfileView> ChangeSpkiPinAsync(
        Guid deviceId,
        Hash256 newPin,
        string actor,
        CancellationToken cancellationToken = default);

    Task<ConnectionProfileView?> GetViewAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);
}
