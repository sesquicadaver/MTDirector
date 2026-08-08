namespace Mfc.RouterOs;

/// <summary>
/// Assembly marker. Read adapter protocol: word-length (M1-06) + sentence codec (M1-07).
/// Write namespace is intentionally absent.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>Preserves Application/Domain project references for boundary analysis.</summary>
    public static Type ApplicationDependencyAnchor { get; } = typeof(Application.AssemblyMarker);

    public static Type DomainDependencyAnchor { get; } = typeof(Domain.AssemblyMarker);

    /// <summary>Roots the word-length codec for architecture and smoke scans.</summary>
    public static Type WordLengthCodecAnchor { get; } = typeof(Protocol.ApiWordLengthCodec);

    /// <summary>Roots the sentence parser for architecture and smoke scans.</summary>
    public static Type SentenceParserAnchor { get; } = typeof(Protocol.ApiSentenceParser);
}
