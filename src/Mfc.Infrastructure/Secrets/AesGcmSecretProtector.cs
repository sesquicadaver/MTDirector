using System.Security.Cryptography;
using Mfc.Application.Abstractions.Secrets;
using Mfc.Infrastructure.Security;

namespace Mfc.Infrastructure.Secrets;

/// <summary>AES-256-GCM envelope encryption with a random per-secret DEK wrapped by <see cref="IMasterKeyProvider"/>.</summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private readonly IMasterKeyProvider _masterKeyProvider;

    public AesGcmSecretProtector(IMasterKeyProvider masterKeyProvider)
    {
        ArgumentNullException.ThrowIfNull(masterKeyProvider);
        _masterKeyProvider = masterKeyProvider;
    }

    public ProtectedSecretMaterial Protect(ReadOnlySpan<byte> plaintextUtf8)
    {
        if (plaintextUtf8.IsEmpty)
        {
            throw new ArgumentException("Secret plaintext must be non-empty.", nameof(plaintextUtf8));
        }

        byte[] dek = RandomNumberGenerator.GetBytes(32);
        try
        {
            byte[] ciphertext = AesGcmEnvelope.Seal(dek, plaintextUtf8);
            byte[] wrappedDek = _masterKeyProvider.WrapDek(dek);
            return new ProtectedSecretMaterial
            {
                Ciphertext = ciphertext,
                WrappedDek = wrappedDek,
                Algorithm = ProtectedSecretMaterial.Aes256GcmAlgorithm,
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public SecretLease Unprotect(ProtectedSecretMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (!string.Equals(material.Algorithm, ProtectedSecretMaterial.Aes256GcmAlgorithm, StringComparison.Ordinal))
        {
            throw new CryptographicException($"Unsupported secret algorithm '{material.Algorithm}'.");
        }

        byte[] dek = _masterKeyProvider.UnwrapDek(material.WrappedDek);
        try
        {
            byte[] plaintext = AesGcmEnvelope.Open(dek, material.Ciphertext);
            return new SecretLease(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }
}
