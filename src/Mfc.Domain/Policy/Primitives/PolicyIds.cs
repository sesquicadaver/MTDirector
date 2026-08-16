namespace Mfc.Domain.Policy.Primitives;

public readonly record struct PolicyId(Guid Value)
{
    public static PolicyId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct PolicyRevisionId(Guid Value)
{
    public static PolicyRevisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Actor identity for policy revision authorship (Policy Model §8 <c>created_by</c>).</summary>
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct AddressObjectId(Guid Value)
{
    public static AddressObjectId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ServiceObjectId(Guid Value)
{
    public static ServiceObjectId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ZoneId(Guid Value)
{
    public static ZoneId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct NodeZoneBindingId(Guid Value)
{
    public static NodeZoneBindingId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Stable identity for a managed filter rule (Policy Model §23); independent of ordinal.</summary>
public readonly record struct RuleId(Guid Value)
{
    public static RuleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Stable identity for a policy test case (Policy Model §54 / M2-16).</summary>
public readonly record struct PolicyTestId(Guid Value)
{
    public static PolicyTestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Immutable analysis-run identity (Policy Model §66 / M2-17).</summary>
public readonly record struct PolicyAnalysisRunId(Guid Value)
{
    public static PolicyAnalysisRunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Append-only approval vote identity (Policy Model §67 / M2-17).</summary>
public readonly record struct PolicyApprovalId(Guid Value)
{
    public static PolicyApprovalId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Desired policy binding identity (Policy Model §10 / M2-17).</summary>
public readonly record struct PolicyBindingId(Guid Value)
{
    public static PolicyBindingId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Warning-acknowledgment identity (Policy Model §66 / M2-17).</summary>
public readonly record struct PolicyWarningAcknowledgmentId(Guid Value)
{
    public static PolicyWarningAcknowledgmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
