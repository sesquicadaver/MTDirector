using Mfc.Domain.Drift.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Drift;

/// <summary>
/// Immutable append-only drift detection event (E2E §34 / M6-02 AC12).
/// Historical Critical events are never mutated; clearing is a later non-blocking event.
/// </summary>
public sealed class DriftEvent
{
    public DriftEventId Id { get; }

    public DeviceId DeviceId { get; }

    public NodeId NodeId { get; }

    public Hash256? BaselineCommittedHash { get; }

    public Hash256? ActualManagedResourceHash { get; }

    public Hash256? DesiredArtifactHashIgnoredForBaseline { get; }

    public DriftOutcome Outcome { get; }

    public bool ConfigurationDriftPresent { get; }

    public bool BlocksDeployment { get; }

    public IReadOnlyList<DriftFinding> Findings { get; }

    public string? SemanticDiffCanonical { get; }

    public Hash256? SemanticDiffHash { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Always true — drift events are append-only.</summary>
    public bool Immutable { get; }

    private DriftEvent(
        DriftEventId id,
        DeviceId deviceId,
        NodeId nodeId,
        Hash256? baselineCommittedHash,
        Hash256? actualManagedResourceHash,
        Hash256? desiredArtifactHashIgnoredForBaseline,
        DriftOutcome outcome,
        bool configurationDriftPresent,
        bool blocksDeployment,
        IReadOnlyList<DriftFinding> findings,
        string? semanticDiffCanonical,
        Hash256? semanticDiffHash,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        DeviceId = deviceId;
        NodeId = nodeId;
        BaselineCommittedHash = baselineCommittedHash;
        ActualManagedResourceHash = actualManagedResourceHash;
        DesiredArtifactHashIgnoredForBaseline = desiredArtifactHashIgnoredForBaseline;
        Outcome = outcome;
        ConfigurationDriftPresent = configurationDriftPresent;
        BlocksDeployment = blocksDeployment;
        Findings = findings;
        SemanticDiffCanonical = semanticDiffCanonical;
        SemanticDiffHash = semanticDiffHash;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Immutable = true;
    }

    /// <summary>Creates a new immutable drift event from an evaluation result.</summary>
    public static DriftEvent Create(
        DeviceId deviceId,
        NodeId nodeId,
        DriftEvaluation evaluation,
        DateTimeOffset createdAtUtc,
        DriftEventId? id = null)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        return new DriftEvent(
            id ?? DriftEventId.New(),
            deviceId,
            nodeId,
            evaluation.BaselineCommittedHash,
            evaluation.ActualManagedResourceHash,
            evaluation.DesiredArtifactHashIgnoredForBaseline,
            evaluation.Outcome,
            evaluation.ConfigurationDriftPresent,
            evaluation.BlocksDeployment,
            evaluation.Findings,
            evaluation.SemanticDiffCanonical,
            evaluation.SemanticDiffHash,
            createdAtUtc);
    }

    /// <summary>Rebuilds an immutable drift event from persistence.</summary>
    public static DriftEvent Reconstitute(
        DriftEventId id,
        DeviceId deviceId,
        NodeId nodeId,
        Hash256? baselineCommittedHash,
        Hash256? actualManagedResourceHash,
        Hash256? desiredArtifactHashIgnoredForBaseline,
        DriftOutcome outcome,
        bool configurationDriftPresent,
        bool blocksDeployment,
        IReadOnlyList<DriftFinding> findings,
        string? semanticDiffCanonical,
        Hash256? semanticDiffHash,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(findings);
        return new DriftEvent(
            id,
            deviceId,
            nodeId,
            baselineCommittedHash,
            actualManagedResourceHash,
            desiredArtifactHashIgnoredForBaseline,
            outcome,
            configurationDriftPresent,
            blocksDeployment,
            findings,
            semanticDiffCanonical,
            semanticDiffHash,
            createdAtUtc);
    }
}
