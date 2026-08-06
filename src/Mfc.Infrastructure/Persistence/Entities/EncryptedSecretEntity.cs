namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Envelope-encrypted secret material. No plaintext column exists by design.
/// </summary>
public sealed class EncryptedSecretEntity
{
    public Guid Id { get; set; }

    public required byte[] Ciphertext { get; set; }

    public required byte[] WrappedDek { get; set; }

    public required string Algorithm { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? RotatedAtUtc { get; set; }
}
