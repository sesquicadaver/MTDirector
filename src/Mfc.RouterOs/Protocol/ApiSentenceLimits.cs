namespace Mfc.RouterOs.Protocol;

/// <summary>Configurable sentence parser/encoder limits (Read Adapter Spec §7.3).</summary>
public sealed class ApiSentenceLimits
{
    public static ApiSentenceLimits Default { get; } = new();

    public int MaxWordPayloadBytes { get; init; } = ApiWordLengthCodec.DefaultMaxWordPayloadBytes;

    public int MaxWordsPerSentence { get; init; } = 256;

    public int MaxSentencePayloadBytes { get; init; } = 2 * 1024 * 1024;

    public int MaxConsecutiveEmptySentences { get; init; } = 16;
}
