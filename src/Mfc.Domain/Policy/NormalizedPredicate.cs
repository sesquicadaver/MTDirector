namespace Mfc.Domain.Policy;

/// <summary>Bounded union of atomic traffic cubes (Policy Model §37).</summary>
public sealed class NormalizedPredicate
{
    private NormalizedPredicate(IReadOnlyList<AtomicTrafficCube> cubes) => Cubes = cubes;

    public IReadOnlyList<AtomicTrafficCube> Cubes { get; }

    public static NormalizedPredicate Empty { get; } = new([]);

    public bool IsEmpty => Cubes.Count == 0;

    /// <summary>
    /// Drops empty cubes. Fails with <see cref="PredicateAlgebraCodes.ComplexityLimit"/> when
    /// <paramref name="limit"/> is exceeded (no unbounded fallback).
    /// </summary>
    public static PredicateAlgebraResult Create(
        IEnumerable<AtomicTrafficCube> cubes,
        int limit = PredicateAlgebraCodes.MaxCubesPerRule)
    {
        ArgumentNullException.ThrowIfNull(cubes);
        if (limit < 1)
        {
            throw new DomainInvariantException("Predicate cube limit must be positive.");
        }

        List<AtomicTrafficCube> kept = cubes.Where(static c => !c.IsEmpty).ToList();
        if (kept.Count > limit)
        {
            return PredicateAlgebraResult.Complexity(
                $"Predicate expansion produced {kept.Count} cubes; limit is {limit}.");
        }

        return PredicateAlgebraResult.Ok(new NormalizedPredicate(kept));
    }
}
