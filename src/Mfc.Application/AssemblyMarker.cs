namespace Mfc.Application;

/// <summary>
/// Assembly marker for architecture tests. Use cases land in later milestones.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>Preserves the Domain project reference for boundary analysis.</summary>
    public static Type DomainDependencyAnchor { get; } = typeof(Domain.AssemblyMarker);
}
