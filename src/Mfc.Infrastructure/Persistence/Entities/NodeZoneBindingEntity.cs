namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Persisted desired Node→zone binding (Policy Model §21; M2-05).</summary>
public sealed class NodeZoneBindingEntity
{
    public Guid Id { get; set; }

    public Guid NodeId { get; set; }

    public Guid ZoneId { get; set; }

    public short Kind { get; set; }

    /// <summary>JSON array of normalized binding values.</summary>
    public required string ValuesJson { get; set; }

    public required byte[] ExpectedDependencyHash { get; set; }

    public byte[]? LastResolvedDependencyHash { get; set; }

    public bool AnalysisStale { get; set; }

    public long RowVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
