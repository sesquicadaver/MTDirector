namespace Mfc.Domain.Drift;

/// <summary>Immutable typed finding produced by managed drift observation.</summary>
public sealed class DriftFinding : IEquatable<DriftFinding>
{
    public DriftFinding(DriftFindingKind kind, string? detail = null)
    {
        Kind = kind;
        Severity = DriftClassifier.Classify(kind);
        Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
    }

    public DriftFindingKind Kind { get; }

    public DriftSeverity Severity { get; }

    public string? Detail { get; }

    public bool Equals(DriftFinding? other)
    {
        if (other is null)
        {
            return false;
        }

        return Kind == other.Kind
               && Severity == other.Severity
               && string.Equals(Detail, other.Detail, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is DriftFinding other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, Severity, Detail);
}
