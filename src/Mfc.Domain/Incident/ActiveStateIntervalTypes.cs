using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Incident;

/// <summary>Historical active-state certainty (next-2 §ActiveStateInterval).</summary>
public enum ActiveStateCertainty
{
    Proven = 1,
    Partial = 2,
    Unknown = 3,
}

/// <summary>One committed-state transition on the device timeline (M7.3-02).</summary>
public sealed class ActiveStateTransitionFact
{
    public required DeviceId DeviceId { get; init; }

    public required DateTimeOffset EffectiveAt { get; init; }

    public Hash256? PolicyHash { get; init; }

    public Hash256? ArtifactHash { get; init; }

    public Hash256? ConfigurationHash { get; init; }

    public Hash256? TopologyHash { get; init; }

    public bool ActualKnown { get; init; }

    public bool AnchorKnown { get; init; }
}

/// <summary>Scripted deployment/audit timeline input for historical resolution (M7.3-02).</summary>
public sealed class ActiveStateTimelineSnapshot
{
    public IReadOnlyList<ActiveStateTransitionFact> Transitions { get; init; } = [];
}

/// <summary>Query for historical active-state resolution at incident occurred_at.</summary>
public sealed class ActiveStateIntervalQuery
{
    public required DeviceId DeviceId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}

public sealed class ActiveStateIntervalFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Resolver output for one occurred_at lookup (M7.3-02).</summary>
public sealed class ActiveStateIntervalResult
{
    public ActiveStateInterval? Interval { get; init; }

    public ActiveStateCertainty Certainty { get; init; }

    public IReadOnlyList<ActiveStateIntervalFinding> Findings { get; init; } = [];
}
