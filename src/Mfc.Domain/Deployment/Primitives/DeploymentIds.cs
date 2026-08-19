namespace Mfc.Domain.Deployment.Primitives;

/// <summary>Immutable deployment plan identity (Safe Deployment Spec §9).</summary>
public readonly record struct DeploymentPlanId(Guid Value)
{
    public static DeploymentPlanId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Node-scoped deployment operation identity (Safe Deployment Spec §13).</summary>
public readonly record struct DeploymentOperationId(Guid Value)
{
    public static DeploymentOperationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Write-ahead journal step identity (Safe Deployment Spec §16).</summary>
public readonly record struct DeploymentStepId(Guid Value)
{
    public static DeploymentStepId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
