namespace Mfc.RouterOs;

/// <summary>
/// Assembly marker. Read adapter: word/sentence/session/API-SSL (M1-06…M1-09).
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

    /// <summary>Roots the tagged session for architecture and smoke scans.</summary>
    public static Type SessionAnchor { get; } = typeof(Session.RosSession);

    /// <summary>Roots the authenticated API-SSL connection for architecture scans.</summary>
    public static Type ApiSslConnectionAnchor { get; } = typeof(Transport.AuthenticatedRosConnection);
}
