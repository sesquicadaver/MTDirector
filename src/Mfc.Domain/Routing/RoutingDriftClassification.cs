namespace Mfc.Domain.Routing;

/// <summary>Result of comparing two routing assurance snapshots (M7.1-09).</summary>
public sealed class RoutingDriftClassification
{
    public bool IsConfigurationDrift { get; init; }

    public bool IsOperationalChange { get; init; }

    public bool ConfigurationHashChanged { get; init; }

    public bool OperationalHashChanged { get; init; }

    public IReadOnlyList<RouteFinding> Findings { get; init; } = [];

    /// <summary>Empty classification when no previous state or hashes are identical.</summary>
    public static RoutingDriftClassification None { get; } = new();
}
