namespace Mfc.RouterOs;

/// <summary>
/// Assembly marker. Read adapter implementation starts in M1; Write namespace is intentionally absent.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>Preserves Application/Domain project references for boundary analysis.</summary>
    public static Type ApplicationDependencyAnchor { get; } = typeof(Application.AssemblyMarker);

    public static Type DomainDependencyAnchor { get; } = typeof(Domain.AssemblyMarker);
}
