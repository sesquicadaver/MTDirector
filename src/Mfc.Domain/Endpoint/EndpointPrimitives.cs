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
