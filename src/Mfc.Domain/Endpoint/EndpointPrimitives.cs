namespace Mfc.Domain.Endpoint;

/// <summary>Stable endpoint identity (M7.2-02).</summary>
public readonly record struct EndpointId(Guid Value)
{
    public static EndpointId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>One presence interval identity for an endpoint at a site/node (M7.2-02).</summary>
public readonly record struct PresenceId(Guid Value)
{
    public static PresenceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>External incident correlation identity (M7.2-03).</summary>
public readonly record struct IncidentId(Guid Value)
{
    public static IncidentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Response assessment identity (M7.2-03).</summary>
public readonly record struct AssessmentId(Guid Value)
{
    public static AssessmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
