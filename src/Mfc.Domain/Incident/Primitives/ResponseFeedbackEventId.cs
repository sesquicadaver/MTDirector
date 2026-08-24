namespace Mfc.Domain.Incident.Primitives;

/// <summary>Strongly typed identifier for immutable response feedback events.</summary>
public readonly record struct ResponseFeedbackEventId(Guid Value)
{
    public static ResponseFeedbackEventId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
