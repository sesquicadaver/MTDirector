namespace Mfc.RouterOs.Protocol;

/// <summary>Typed protocol fault codes for RouterOS API framing (Read Adapter Spec §6–9).</summary>
public sealed record RouterOsProtocolError(string Code, string Message)
{
    public const string LengthEncodingNonCanonical = "API_LENGTH_ENCODING_NON_CANONICAL";

    public const string LengthPrefixUnsupported = "API_LENGTH_PREFIX_UNSUPPORTED";

    public const string ReservedControlByte = "API_RESERVED_CONTROL_BYTE";

    public const string LengthTruncated = "API_LENGTH_TRUNCATED";

    public const string WordTooLarge = "API_WORD_TOO_LARGE";

    public const string IntegerOverflow = "API_LENGTH_INTEGER_OVERFLOW";

    public const string AttributeMalformed = "API_ATTRIBUTE_MALFORMED";

    public const string DuplicateAttribute = "API_DUPLICATE_ATTRIBUTE";

    public const string SentenceTooLarge = "API_SENTENCE_TOO_LARGE";

    public const string TooManyWords = "API_TOO_MANY_WORDS";

    public const string ParserFaulted = "API_PARSER_FAULTED";

    public const string UnexpectedEndOfStream = "API_UNEXPECTED_END_OF_STREAM";

    public const string InvalidCommandWord = "API_INVALID_COMMAND_WORD";

    public const string UnknownReplyTag = "API_UNKNOWN_REPLY_TAG";

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

    public static RouterOsProtocolError MalformedAttribute(string detail) =>
        new(AttributeMalformed, detail);

    public static RouterOsProtocolError Duplicate(string detail) =>
        new(DuplicateAttribute, detail);

    public static RouterOsProtocolError SentenceTooLargeError(string detail) =>
        new(SentenceTooLarge, detail);

    public static RouterOsProtocolError TooManyWordsError(int max) =>
        new(TooManyWords, $"Sentence exceeds maximum of {max} words.");

    public static RouterOsProtocolError AlreadyFaulted() =>
        new(ParserFaulted, "Parser is FAULTED and cannot be reused.");

    public static RouterOsProtocolError UnexpectedEof(string detail) =>
        new(UnexpectedEndOfStream, detail);

    public static RouterOsProtocolError InvalidCommand(string detail) =>
        new(InvalidCommandWord, detail);
}
