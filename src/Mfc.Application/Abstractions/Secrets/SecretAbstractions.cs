using System.Security.Cryptography;

namespace Mfc.Application.Abstractions.Secrets;

/// <summary>Wraps/unwraps per-secret DEKs with a master key held outside PostgreSQL.</summary>
public interface IMasterKeyProvider
{
    string Name { get; }

    byte[] WrapDek(ReadOnlySpan<byte> dek);

    byte[] UnwrapDek(ReadOnlySpan<byte> wrappedDek);
}

/// <summary>Envelope-encrypted secret material ready for persistence.</summary>
public sealed class ProtectedSecretMaterial
{
    public const string Aes256GcmAlgorithm = "AES-256-GCM";

    public required byte[] Ciphertext { get; init; }

    public required byte[] WrappedDek { get; init; }

    public required string Algorithm { get; init; }
}

/// <summary>Short-lived plaintext lease; zeros memory on dispose.</summary>
public sealed class SecretLease : IDisposable
{
    private byte[]? _plaintext;

    public SecretLease(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        _plaintext = plaintext;
    }

    public ReadOnlySpan<byte> Plaintext
        => _plaintext ?? throw new ObjectDisposedException(nameof(SecretLease));

    public void Dispose()
    {
        if (_plaintext is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_plaintext);
        _plaintext = null;
    }
}

/// <summary>Encrypts RouterOS credentials before persistence; decrypts into a disposable lease.</summary>
public interface ISecretProtector
{
    ProtectedSecretMaterial Protect(ReadOnlySpan<byte> plaintextUtf8);

    SecretLease Unprotect(ProtectedSecretMaterial material);
}
