using Mfc.Domain.Drift;

namespace Mfc.Application.Models;

/// <summary>Application view of one typed drift finding.</summary>
public sealed class DriftFindingView
{
    public required DriftFindingKind Kind { get; init; }

    public required DriftSeverity Severity { get; init; }

    public string? Detail { get; init; }
}

/// <summary>Application view of an immutable drift event (M6-02).</summary>
public sealed class DriftEventView
{
    public required Guid Id { get; init; }

    public required Guid DeviceId { get; init; }

    public required Guid NodeId { get; init; }

    public string? BaselineCommittedHashHex { get; init; }

    public string? ActualManagedResourceHashHex { get; init; }

    public string? DesiredArtifactHashIgnoredForBaselineHex { get; init; }

    public required DriftOutcome Outcome { get; init; }

    public required bool ConfigurationDriftPresent { get; init; }

    public required bool BlocksDeployment { get; init; }

    public required IReadOnlyList<DriftFindingView> Findings { get; init; }

    public string? SemanticDiffCanonical { get; init; }

    public string? SemanticDiffHashHex { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required bool Immutable { get; init; }
}
