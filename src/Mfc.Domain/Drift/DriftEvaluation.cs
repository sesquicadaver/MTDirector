using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Drift;

/// <summary>Immutable result of <see cref="ManagedDriftDetector.Evaluate"/>.</summary>
public sealed class DriftEvaluation
{
    public DriftEvaluation(
        DriftOutcome outcome,
        bool configurationDriftPresent,
        bool blocksDeployment,
        Hash256? baselineCommittedHash,
        Hash256? actualManagedResourceHash,
        Hash256? desiredArtifactHashIgnoredForBaseline,
        IReadOnlyList<DriftFinding> findings,
        string? semanticDiffCanonical,
        Hash256? semanticDiffHash)
    {
        ArgumentNullException.ThrowIfNull(findings);
        Outcome = outcome;
        ConfigurationDriftPresent = configurationDriftPresent;
        BlocksDeployment = blocksDeployment;
        BaselineCommittedHash = baselineCommittedHash;
        ActualManagedResourceHash = actualManagedResourceHash;
        DesiredArtifactHashIgnoredForBaseline = desiredArtifactHashIgnoredForBaseline;
        Findings = findings;
        SemanticDiffCanonical = semanticDiffCanonical;
        SemanticDiffHash = semanticDiffHash;
    }

    public DriftOutcome Outcome { get; }

    /// <summary>True when actual managed resources diverge from last committed artifact.</summary>
    public bool ConfigurationDriftPresent { get; }

    /// <summary>True when Critical configuration drift must block a new deployment.</summary>
    public bool BlocksDeployment { get; }

    public Hash256? BaselineCommittedHash { get; }

    public Hash256? ActualManagedResourceHash { get; }

    /// <summary>
    /// Desired hash was supplied only for pending-deploy discrimination; never used as the drift baseline.
    /// </summary>
    public Hash256? DesiredArtifactHashIgnoredForBaseline { get; }

    public IReadOnlyList<DriftFinding> Findings { get; }

    public string? SemanticDiffCanonical { get; }

    public Hash256? SemanticDiffHash { get; }
}
