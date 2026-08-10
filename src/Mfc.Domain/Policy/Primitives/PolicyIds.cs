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
