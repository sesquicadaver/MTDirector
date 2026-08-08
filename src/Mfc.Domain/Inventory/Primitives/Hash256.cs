namespace Mfc.Domain.Inventory.Primitives;

/// <summary>
/// 32-byte SHA-256 digest for dependency / content hashes.
/// </summary>
public sealed class Hash256 : IEquatable<Hash256>
{
    public const int Size = 32;

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
