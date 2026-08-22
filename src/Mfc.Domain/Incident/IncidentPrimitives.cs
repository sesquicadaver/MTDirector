namespace Mfc.Domain.Incident;

/// <summary>Stable normalized event identity for ingress correlation (next-2 §IncidentSignal).</summary>
public readonly record struct EventId(Guid Value)
{
    public static EventId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
