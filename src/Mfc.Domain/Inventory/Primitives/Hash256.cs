namespace Mfc.Domain.Inventory.Primitives;

/// <summary>
/// 32-byte SHA-256 digest for dependency / content hashes.
/// Algorithm is fixed: SHA-256 only; length is always <see cref="Size"/> bytes.
/// </summary>
public sealed class Hash256 : IEquatable<Hash256>
{
    public const int Size = 32;

    public const string AlgorithmName = "SHA-256";

    private readonly byte[] _bytes;

    private Hash256(byte[] bytes) => _bytes = bytes;

    public ReadOnlySpan<byte> Bytes => _bytes;

    public static Hash256 Create(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new DomainInvariantException("Hash256 requires exactly 32 bytes.");
        }

        return new Hash256(bytes.ToArray());
    }

    /// <summary>
    /// Parses a 64-character hex digest (optional <c>0x</c> prefix). Rejects any other text.
    /// </summary>
    public static Hash256 ParseHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        string normalized = hex.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        if (normalized.Length != Size * 2)
        {
            throw new DomainInvariantException(
                $"Hash256 hex must be exactly {Size * 2} hexadecimal characters.");
        }

        foreach (char c in normalized)
        {
            bool isHex = (c is >= '0' and <= '9')
                         || (c is >= 'a' and <= 'f')
                         || (c is >= 'A' and <= 'F');
            if (!isHex)
            {
                throw new DomainInvariantException("Hash256 hex contains invalid characters.");
            }
        }

        return Create(Convert.FromHexString(normalized));
    }

    public bool Equals(Hash256? other)
        => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => obj is Hash256 other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hc = default;
        foreach (byte b in _bytes)
        {
            hc.Add(b);
        }

        return hc.ToHashCode();
    }

    public override string ToString() => Convert.ToHexString(_bytes).ToLowerInvariant();
}
