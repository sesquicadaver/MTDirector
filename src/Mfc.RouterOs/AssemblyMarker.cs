namespace Mfc.RouterOs;

/// <summary>
/// Assembly marker. Read adapter through interface/address discovery (M1-06…M1-12).
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

    /// <summary>Roots the allowlisted read executor for architecture scans.</summary>
    public static Type ReadCommandExecutorAnchor { get; } = typeof(Commands.RosReadCommandExecutor);

    /// <summary>Roots system/service discovery for architecture scans.</summary>
    public static Type SystemServiceDiscoveryAnchor { get; } = typeof(Discovery.SystemServiceDiscovery);

    /// <summary>Roots interface/address discovery for architecture scans.</summary>
    public static Type InterfaceAddressDiscoveryAnchor { get; } = typeof(Discovery.InterfaceAddressDiscovery);
}
