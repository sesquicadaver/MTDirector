using System.Buffers.Binary;

namespace Mfc.RouterOs.Protocol;

/// <summary>
/// Outcome of a length-prefix decode attempt that may span fragmented TCP input.
/// </summary>
public enum ApiLengthDecodeStatus : byte
{
    /// <summary>Length was decoded and is within the configured maximum.</summary>
    Success = 0,

    /// <summary>Need more bytes before the prefix can be decoded (not a fault).</summary>
    NeedMoreData = 1,

    /// <summary>Protocol violation; session must transition to FAULTED.</summary>
    Faulted = 2,
}

/// <summary>
/// Canonical RouterOS API word-length encoder/decoder (network byte order).
/// Spec: RouterOS Read Adapter Specification §6.
/// </summary>
public static class ApiWordLengthCodec
{
    /// <summary>Production default maximum word payload (256 KiB).</summary>
    public const int DefaultMaxWordPayloadBytes = 256 * 1024;

    /// <summary>Sentinel for decode tests that exercise the full uint length space.</summary>
    public const uint UnlimitedMaxWordPayloadBytes = uint.MaxValue;

    private const uint OneByteMax = 0x7F;
    private const uint TwoByteMax = 0x3FFF;
    private const uint ThreeByteMax = 0x1FFFFF;
    private const uint FourByteMax = 0x0FFFFFFF;

    private const uint TwoByteFlag = 0x8000;
    private const uint ThreeByteFlag = 0xC00000;
    private const uint FourByteFlag = 0xE0000000;
    private const byte FiveBytePrefix = 0xF0;

    /// <summary>Returns the canonical encoded prefix size for <paramref name="length"/>.</summary>
    public static int GetEncodedPrefixLength(uint length) => length switch
    {
        <= OneByteMax => 1,
        <= TwoByteMax => 2,
        <= ThreeByteMax => 3,
        <= FourByteMax => 4,
        _ => 5,
    };

    /// <summary>
    /// Encodes <paramref name="length"/> into the shortest canonical network-order prefix.
    /// Host endianness is irrelevant — writes are explicit big-endian.
    /// </summary>
    /// <returns>Number of bytes written.</returns>
    public static int Encode(uint length, Span<byte> destination)
    {
        int needed = GetEncodedPrefixLength(length);
        if (destination.Length < needed)
        {
            throw new ArgumentException(
                $"Destination span length {destination.Length} is shorter than required {needed}.",
                nameof(destination));
        }

        switch (needed)
        {
            case 1:
                destination[0] = (byte)length;
                return 1;
            case 2:
                BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)(length | TwoByteFlag));
                return 2;
            case 3:
                {
                    uint encoded = length | ThreeByteFlag;
                    destination[0] = (byte)(encoded >> 16);
                    destination[1] = (byte)(encoded >> 8);
                    destination[2] = (byte)encoded;
                    return 3;
                }

            case 4:
                BinaryPrimitives.WriteUInt32BigEndian(destination, length | FourByteFlag);
                return 4;
            default:
                destination[0] = FiveBytePrefix;
                BinaryPrimitives.WriteUInt32BigEndian(destination[1..], length);
                return 5;
        }
    }

    /// <summary>
    /// Attempts to decode a length prefix from possibly fragmented input.
    /// Rejects non-canonical encodings, reserved control bytes, and lengths above
    /// <paramref name="maxWordPayloadBytes"/> before any word-body allocation.
    /// </summary>
    public static ApiLengthDecodeStatus TryDecode(
        ReadOnlySpan<byte> source,
        uint maxWordPayloadBytes,
        out uint length,
        out int bytesConsumed,
        out RouterOsProtocolError? error)
    {
        length = 0;
        bytesConsumed = 0;
        error = null;

        if (source.IsEmpty)
        {
            return ApiLengthDecodeStatus.NeedMoreData;
        }

        byte first = source[0];
        if (first <= OneByteMax)
        {
            return Complete(first, prefixLength: 1, maxWordPayloadBytes, out length, out bytesConsumed, out error);
        }

        if (first is >= 0xF8 and <= 0xFF)
        {
            error = RouterOsProtocolError.ReservedControl(first);
            return ApiLengthDecodeStatus.Faulted;
        }

        if (first is >= 0xF1 and <= 0xF7)
        {
            error = RouterOsProtocolError.UnsupportedPrefix(first);
            return ApiLengthDecodeStatus.Faulted;
        }

        if (first == FiveBytePrefix)
        {
            if (source.Length < 5)
            {
                return ApiLengthDecodeStatus.NeedMoreData;
            }

            uint decoded = BinaryPrimitives.ReadUInt32BigEndian(source[1..]);
            if (decoded < 0x10000000u)
            {
                error = RouterOsProtocolError.NonCanonical(
                    "Five-byte length encoding used for length below 0x10000000.");
                return ApiLengthDecodeStatus.Faulted;
            }

            return Complete(decoded, prefixLength: 5, maxWordPayloadBytes, out length, out bytesConsumed, out error);
        }

        if ((first & 0xF0) == 0xE0)
        {
            if (source.Length < 4)
            {
                return ApiLengthDecodeStatus.NeedMoreData;
            }

            uint encoded = BinaryPrimitives.ReadUInt32BigEndian(source);
            uint decoded = encoded & ~FourByteFlag;
            if (decoded < 0x200000u)
            {
                error = RouterOsProtocolError.NonCanonical(
                    "Four-byte length encoding used for length below 0x200000.");
                return ApiLengthDecodeStatus.Faulted;
            }

            return Complete(decoded, prefixLength: 4, maxWordPayloadBytes, out length, out bytesConsumed, out error);
        }

        if ((first & 0xE0) == 0xC0)
        {
            if (source.Length < 3)
            {
                return ApiLengthDecodeStatus.NeedMoreData;
            }

            uint encoded = ((uint)source[0] << 16) | ((uint)source[1] << 8) | source[2];
            uint decoded = encoded & ~ThreeByteFlag;
            if (decoded < 0x4000u)
            {
                error = RouterOsProtocolError.NonCanonical(
                    "Three-byte length encoding used for length below 0x4000.");
                return ApiLengthDecodeStatus.Faulted;
            }

            return Complete(decoded, prefixLength: 3, maxWordPayloadBytes, out length, out bytesConsumed, out error);
        }

        // first is in 0x80..0xBF (10xxxxxx) — the only remaining legal multi-byte class.
        if (source.Length < 2)
        {
            return ApiLengthDecodeStatus.NeedMoreData;
        }

        ushort twoByte = BinaryPrimitives.ReadUInt16BigEndian(source);
        uint twoDecoded = (uint)(twoByte & ~TwoByteFlag);
        if (twoDecoded < 0x80u)
        {
            error = RouterOsProtocolError.NonCanonical(
                "Two-byte length encoding used for length below 0x80.");
            return ApiLengthDecodeStatus.Faulted;
        }

        return Complete(twoDecoded, prefixLength: 2, maxWordPayloadBytes, out length, out bytesConsumed, out error);
    }

    private static ApiLengthDecodeStatus Complete(
        uint decoded,
        int prefixLength,
        uint maxWordPayloadBytes,
        out uint length,
        out int bytesConsumed,
        out RouterOsProtocolError? error)
    {
        length = 0;
        bytesConsumed = 0;
        error = null;

        if (decoded > maxWordPayloadBytes)
        {
            error = RouterOsProtocolError.TooLarge(decoded, maxWordPayloadBytes);
            return ApiLengthDecodeStatus.Faulted;
        }

        length = decoded;
        bytesConsumed = prefixLength;
        return ApiLengthDecodeStatus.Success;
    }
}
