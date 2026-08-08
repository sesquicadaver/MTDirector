namespace Mfc.RouterOs.Protocol;

/// <summary>Typed protocol fault codes for RouterOS API framing (Read Adapter Spec §6.3).</summary>
public sealed record RouterOsProtocolError(string Code, string Message)
{
    public const string LengthEncodingNonCanonical = "API_LENGTH_ENCODING_NON_CANONICAL";

    public const string LengthPrefixUnsupported = "API_LENGTH_PREFIX_UNSUPPORTED";

    public const string ReservedControlByte = "API_RESERVED_CONTROL_BYTE";

    public const string LengthTruncated = "API_LENGTH_TRUNCATED";

    public const string WordTooLarge = "API_WORD_TOO_LARGE";

    public const string IntegerOverflow = "API_LENGTH_INTEGER_OVERFLOW";

    public static RouterOsProtocolError NonCanonical(string detail) =>
        new(LengthEncodingNonCanonical, detail);

    public static RouterOsProtocolError UnsupportedPrefix(byte prefix) =>
        new(LengthPrefixUnsupported, $"Unsupported length prefix 0x{prefix:X2}.");

    public static RouterOsProtocolError ReservedControl(byte prefix) =>
        new(ReservedControlByte, $"Reserved control byte 0x{prefix:X2}.");

    public static RouterOsProtocolError Truncated(string detail) =>
        new(LengthTruncated, detail);

    public static RouterOsProtocolError TooLarge(uint length, uint maxBytes) =>
        new(WordTooLarge, $"Decoded word length {length} exceeds configured maximum {maxBytes}.");

    public static RouterOsProtocolError Overflow(string detail) =>
        new(IntegerOverflow, detail);
}
