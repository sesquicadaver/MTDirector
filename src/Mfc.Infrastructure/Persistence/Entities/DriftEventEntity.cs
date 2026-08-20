namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Append-only persisted drift detection event (M6-02). Findings + semantic diff as JSON/text.</summary>
public sealed class DriftEventEntity
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Guid NodeId { get; set; }

    public byte[]? BaselineCommittedHash { get; set; }

    public byte[]? ActualManagedResourceHash { get; set; }

    public byte[]? DesiredArtifactHashIgnoredForBaseline { get; set; }

    public short Outcome { get; set; }

    public bool ConfigurationDriftPresent { get; set; }

    public bool BlocksDeployment { get; set; }

    /// <summary>JSON array of { kind, severity, detail }.</summary>
    public string FindingsJson { get; set; } = "[]";

    public string? SemanticDiffCanonical { get; set; }

    public byte[]? SemanticDiffHash { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Always true; column exists so immutability is queryable and enforced.</summary>
    public bool Immutable { get; set; } = true;
}
