namespace Mfc.Application;

/// <summary>
/// Assembly marker for architecture tests. Ports for secrets/connection profiles land in M1-04;
/// inventory use cases continue in M1-05+.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>Preserves the Domain project reference for boundary analysis.</summary>
    public static Type DomainDependencyAnchor { get; } = typeof(Domain.AssemblyMarker);

    /// <summary>Keeps secret port types rooted in Application for architecture scans.</summary>
    public static Type SecretPortAnchor { get; } = typeof(Abstractions.Secrets.ISecretProtector);
}
