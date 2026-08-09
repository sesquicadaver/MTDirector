using System.IO.Compression;
using System.Security.Cryptography;
using Mfc.Domain.Snapshots;

namespace Mfc.Infrastructure.Persistence.Snapshots;

/// <summary>
/// Content-addressed snapshot payload codec: SHA-256 over uncompressed bytes, then Brotli (M1-23 / Canonical §28.4).
/// Compression never enters the content hash.
/// </summary>
public static class BrotliPayloadCodec
{
    /// <summary>Matches <c>ck_snapshot_payload_size</c> upper bound (256 MiB).</summary>
    public const long MaxUncompressedBytes = 268_435_456;

    /// <summary>Encoded payload ready for <c>snapshot_payloads</c> insert.</summary>
    public sealed class EncodedPayload
    {
        public required byte[] PayloadHash { get; init; }

        public required byte[] CompressedPayload { get; init; }

        public required long UncompressedSize { get; init; }

        public required SnapshotCompression Compression { get; init; }
    }

    /// <summary>
    /// Hashes uncompressed bytes with SHA-256, then Brotli-compresses them.
    /// </summary>
    public static EncodedPayload Encode(ReadOnlyMemory<byte> uncompressed)
    {
        if (uncompressed.Length == 0)
        {
            throw new ArgumentException("Snapshot payload must be non-empty.", nameof(uncompressed));
        }

        if (uncompressed.Length > MaxUncompressedBytes)
        {
            throw new ArgumentException(
                $"Snapshot payload exceeds max uncompressed size of {MaxUncompressedBytes} bytes.",
                nameof(uncompressed));
        }

        byte[] hash = SHA256.HashData(uncompressed.Span);
        byte[] compressed = CompressBrotli(uncompressed.Span);

        return new EncodedPayload
        {
            PayloadHash = hash,
            CompressedPayload = compressed,
            UncompressedSize = uncompressed.Length,
            Compression = SnapshotCompression.Brotli,
        };
    }

    /// <summary>
    /// Decompresses a stored payload and verifies the SHA-256 of uncompressed bytes matches <paramref name="expectedHash"/>.
    /// </summary>
    public static byte[] DecodeAndVerify(
        ReadOnlySpan<byte> compressed,
        SnapshotCompression compression,
        long uncompressedSize,
        ReadOnlySpan<byte> expectedHash)
    {
        if (expectedHash.Length != 32)
        {
            throw new ArgumentException("Payload hash must be 32 bytes.", nameof(expectedHash));
        }

        if (uncompressedSize <= 0 || uncompressedSize > MaxUncompressedBytes)
        {
            throw new InvalidOperationException(
                $"Stored uncompressed size {uncompressedSize} is outside the allowed range.");
        }

        byte[] uncompressed = compression switch
        {
            SnapshotCompression.None => compressed.ToArray(),
            SnapshotCompression.Brotli => DecompressBrotli(compressed, checked((int)uncompressedSize)),
            _ => throw new InvalidOperationException($"Unsupported snapshot compression '{compression}'."),
        };

        if (uncompressed.LongLength != uncompressedSize)
        {
            throw new InvalidOperationException(
                "Decompressed payload size does not match stored UncompressedSize.");
        }

        byte[] actualHash = SHA256.HashData(uncompressed);
        if (!actualHash.AsSpan().SequenceEqual(expectedHash))
        {
            throw new InvalidOperationException(
                "Snapshot payload integrity check failed: SHA-256 of decompressed bytes does not match PayloadHash.");
        }

        return uncompressed;
    }

    private static byte[] CompressBrotli(ReadOnlySpan<byte> uncompressed)
    {
        using MemoryStream output = new();
        using (BrotliStream brotli = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotli.Write(uncompressed);
        }

        return output.ToArray();
    }

    private static byte[] DecompressBrotli(ReadOnlySpan<byte> compressed, int uncompressedSize)
    {
        byte[] buffer = new byte[uncompressedSize];
        using MemoryStream input = new(compressed.ToArray(), writable: false);
        using BrotliStream brotli = new(input, CompressionMode.Decompress);
        int read = 0;
        while (read < buffer.Length)
        {
            int n = brotli.Read(buffer, read, buffer.Length - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        if (read != buffer.Length)
        {
            throw new InvalidOperationException(
                "Brotli decompression produced fewer bytes than UncompressedSize.");
        }

        // Ensure no trailing garbage remains in the compressed stream.
        Span<byte> leftover = stackalloc byte[1];
        if (brotli.Read(leftover) > 0)
        {
            throw new InvalidOperationException(
                "Brotli decompression produced more bytes than UncompressedSize.");
        }

        return buffer;
    }
}
