namespace Mfc.Domain.Policy;

/// <summary>Frozen predicate-algebra codes and Spec §37.3 limits (M2-09).</summary>
public static class PredicateAlgebraCodes
{
    public const string ComplexityLimit = "PREDICATE_COMPLEXITY_LIMIT";

    public const int MaxCubesPerRule = 128;

    public const int MaxResidualFragments = 4096;
}

/// <summary>Success or bounded-algebra failure (<see cref="PredicateAlgebraCodes.ComplexityLimit"/>).</summary>
public sealed class PredicateAlgebraResult
{
    private PredicateAlgebraResult(bool isSuccess, string? code, string? message, NormalizedPredicate? value)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
        Value = value;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? Code { get; }

    public string? Message { get; }

    public NormalizedPredicate? Value { get; }

    public static PredicateAlgebraResult Ok(NormalizedPredicate value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new PredicateAlgebraResult(true, null, null, value);
    }

    public static PredicateAlgebraResult Fail(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new PredicateAlgebraResult(false, code, message, null);
    }

    public static PredicateAlgebraResult Complexity(string message)
        => Fail(PredicateAlgebraCodes.ComplexityLimit, message);
}

/// <summary>Spec §37.2 relations. Managed predicates never yield indeterminate.</summary>
public enum PredicateRelation : byte
{
    Empty = 0,
    Equal = 1,
    Disjoint = 2,
    Subset = 3,
    Superset = 4,
    PartialOverlap = 5,
}
