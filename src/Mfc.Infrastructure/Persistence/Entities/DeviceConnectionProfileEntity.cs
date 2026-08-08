namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Connection metadata for a device. Password material lives only in <see cref="EncryptedSecretEntity"/>.
/// Full credential lifecycle is M1-04; schema lands with M1-03 (Vertical Slice §8.5).
/// </summary>
public sealed class DeviceConnectionProfileEntity
{
    public Guid DeviceId { get; set; }

    public required string Username { get; set; }

    public Guid EncryptedSecretId { get; set; }

    public short TrustMode { get; set; }

    public string? CaProfileRef { get; set; }

    public byte[]? PinnedSpkiSha256 { get; set; }

    public int ConnectTimeoutMs { get; set; }

    public int CommandTimeoutMs { get; set; }

    public long MaxResponseBytes { get; set; }

    public long RowVersion { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
