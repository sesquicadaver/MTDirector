namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Immutable completed analysis run (Policy Model §66–§67 / M2-17).</summary>
public sealed class PolicyAnalysisRunEntity
{
    public Guid Id { get; set; }

    public Guid RevisionId { get; set; }

    public required byte[] RevisionContentHash { get; set; }

    public required byte[] LogicalEffectiveHash { get; set; }

    public required byte[] AnalysisContextHash { get; set; }

    public required byte[] EvidenceContextHash { get; set; }

    public required byte[] TopologyProjectionHash { get; set; }

    public required byte[] ImpactSetHash { get; set; }

    public required byte[] PerDeviceAnalysisHashes { get; set; }

    public required byte[] BundleHash { get; set; }

    public required byte[] DependencyFingerprint { get; set; }

    public required string RiskLevel { get; set; }

    public bool EvidenceSignalsPresent { get; set; }

    public required string AnalyzerVersion { get; set; }

    public required string PolicySchemaVersion { get; set; }

    public required string PipelineVersion { get; set; }

    public required string FindingsJson { get; set; }

    public required string TestResultsJson { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
