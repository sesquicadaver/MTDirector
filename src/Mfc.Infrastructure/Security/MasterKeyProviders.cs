using System.Security.Cryptography;
using Mfc.Application.Abstractions.Secrets;

namespace Mfc.Infrastructure.Security;

/// <summary>
/// Development-only master key derived from a fixed label. Forbidden outside Development by host validation.
/// </summary>
public sealed class DevelopmentMasterKeyProvider : IMasterKeyProvider
{
    private readonly byte[] _masterKey;

    public DevelopmentMasterKeyProvider()
    {
        _masterKey = SHA256.HashData("mfc-development-master-key-v1"u8);
    }

    public string Name => "Development";

    public byte[] WrapDek(ReadOnlySpan<byte> dek) => AesGcmEnvelope.Wrap(_masterKey, dek);

    public byte[] UnwrapDek(ReadOnlySpan<byte> wrappedDek) => AesGcmEnvelope.Unwrap(_masterKey, wrappedDek);
}

/// <summary>
/// Production-oriented master key loaded from <c>MFC__Security__MasterKeyBase64</c> (32-byte key).
/// The key never enters PostgreSQL or application settings files in the repository.
/// </summary>
public sealed class EnvironmentMasterKeyProvider : IMasterKeyProvider
{
    public const string EnvironmentVariableName = "MFC__Security__MasterKeyBase64";

    private readonly byte[] _masterKey;

    public EnvironmentMasterKeyProvider(string? masterKeyBase64 = null)
    {
        string? value = masterKeyBase64 ?? Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Master key provider '{Name}' requires environment variable {EnvironmentVariableName} (base64 of 32 bytes).");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(value.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} is not valid base64.", ex);
        }

        if (key.Length != 32)
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must decode to exactly 32 bytes.");
        }

        _masterKey = key;
    }

    public string Name => "OsKeyStore";

    public byte[] WrapDek(ReadOnlySpan<byte> dek) => AesGcmEnvelope.Wrap(_masterKey, dek);

    public byte[] UnwrapDek(ReadOnlySpan<byte> wrappedDek) => AesGcmEnvelope.Unwrap(_masterKey, wrappedDek);
}

/// <summary>Shared AES-256-GCM helpers for DEK wrap and secret body encryption.</summary>
internal static class AesGcmEnvelope
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static byte[] Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];
        using AesGcm aes = new(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        byte[] result = new byte[NonceSize + ciphertext.Length + TagSize];
        nonce.CopyTo(result.AsSpan(0, NonceSize));
        ciphertext.CopyTo(result.AsSpan(NonceSize, ciphertext.Length));
        tag.CopyTo(result.AsSpan(NonceSize + ciphertext.Length, TagSize));
        return result;
    }

    public static byte[] Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> sealedPayload)
    {
        if (sealedPayload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Sealed payload is truncated.");
        }

        ReadOnlySpan<byte> nonce = sealedPayload[..NonceSize];
        ReadOnlySpan<byte> tag = sealedPayload[^TagSize..];
        ReadOnlySpan<byte> ciphertext = sealedPayload[NonceSize..^TagSize];
        byte[] plaintext = new byte[ciphertext.Length];
        using AesGcm aes = new(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    public static byte[] Wrap(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> dek)
        => Seal(masterKey, dek);

    public static byte[] Unwrap(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> wrappedDek)
        => Open(masterKey, wrappedDek);
}
