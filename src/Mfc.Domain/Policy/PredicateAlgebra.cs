namespace Mfc.Domain.Policy;

/// <summary>
/// Bounded symbolic evaluator of managed packet space (Policy Model §37).
/// Never returns indeterminate. Overflow is <see cref="PredicateAlgebraCodes.ComplexityLimit"/>.
/// </summary>
public static class PredicateAlgebra
{
    /// <summary>Classifies <paramref name="left"/> relative to <paramref name="right"/>.</summary>
    public static PredicateRelation Relate(NormalizedPredicate left, NormalizedPredicate right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.IsEmpty && right.IsEmpty)
        {
            return PredicateRelation.Empty;
        }

        if (left.IsEmpty)
        {
            return PredicateRelation.Subset;
        }

        if (right.IsEmpty)
        {
            return PredicateRelation.Superset;
        }

        bool subset = IsSubset(left, right);
        bool superset = IsSubset(right, left);
        if (subset && superset)
        {
            return PredicateRelation.Equal;
        }

        if (subset)
        {
            return PredicateRelation.Subset;
        }

        if (superset)
        {
            return PredicateRelation.Superset;
        }

        return Overlaps(left, right) ? PredicateRelation.PartialOverlap : PredicateRelation.Disjoint;
    }

    /// <summary>
    /// Fail-closed coverage: each left cube must be a subset of some single right cube.
    /// Split covers across several right cubes are treated as not-subset.
    /// </summary>
    public static bool IsSubset(NormalizedPredicate inner, NormalizedPredicate cover)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(cover);
        if (inner.IsEmpty)
        {
            return true;
        }

        if (cover.IsEmpty)
        {
            return false;
        }

        foreach (AtomicTrafficCube cube in inner.Cubes)
        {
            bool covered = false;
            foreach (AtomicTrafficCube other in cover.Cubes)
            {
                if (AtomicTrafficCube.IsSubset(cube, other))
                {
                    covered = true;
                    break;
                }
            }

            if (!covered)
            {
                return false;
            }
        }

        return true;
    }

    public static bool Overlaps(NormalizedPredicate left, NormalizedPredicate right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        foreach (AtomicTrafficCube a in left.Cubes)
        {
            foreach (AtomicTrafficCube b in right.Cubes)
            {
                if (AtomicTrafficCube.Overlaps(a, b))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static PredicateAlgebraResult Union(NormalizedPredicate left, NormalizedPredicate right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return NormalizedPredicate.Create(
            left.Cubes.Concat(right.Cubes),
            PredicateAlgebraCodes.MaxResidualFragments);
    }

    public static PredicateAlgebraResult Intersect(NormalizedPredicate left, NormalizedPredicate right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        List<AtomicTrafficCube> cubes = [];
        foreach (AtomicTrafficCube a in left.Cubes)
        {
            foreach (AtomicTrafficCube b in right.Cubes)
            {
                AtomicTrafficCube? inter = AtomicTrafficCube.Intersect(a, b);
                if (inter is not null)
                {
                    cubes.Add(inter);
                }
            }
        }

        return NormalizedPredicate.Create(cubes, PredicateAlgebraCodes.MaxResidualFragments);
    }

    public static PredicateAlgebraResult Subtract(NormalizedPredicate include, NormalizedPredicate exclude)
    {
        ArgumentNullException.ThrowIfNull(include);
        ArgumentNullException.ThrowIfNull(exclude);
        if (include.IsEmpty)
        {
            return PredicateAlgebraResult.Ok(NormalizedPredicate.Empty);
        }

        if (exclude.IsEmpty)
        {
            return PredicateAlgebraResult.Ok(include);
        }

        List<AtomicTrafficCube> current = include.Cubes.ToList();
        foreach (AtomicTrafficCube cut in exclude.Cubes)
        {
            List<AtomicTrafficCube> next = [];
            foreach (AtomicTrafficCube piece in current)
            {
                next.AddRange(AtomicTrafficCube.Subtract(piece, cut));
                if (next.Count > PredicateAlgebraCodes.MaxResidualFragments)
                {
                    return PredicateAlgebraResult.Complexity(
                        "Predicate subtraction exceeded the residual fragment limit.");
                }
            }

            current = next;
        }

        return NormalizedPredicate.Create(current, PredicateAlgebraCodes.MaxResidualFragments);
    }
}
