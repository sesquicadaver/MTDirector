namespace Mfc.Domain.Drift.Primitives;

/// <summary>Immutable drift event identity (M6-02).</summary>
public readonly record struct DriftEventId(Guid Value)
{
    public static DriftEventId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
