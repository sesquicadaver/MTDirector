using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Include/exclude address selector (Policy Model §17).</summary>
public sealed class AddressSelector
{
    public IReadOnlyList<AddressObjectId> Include { get; }

    public IReadOnlyList<AddressObjectId> Exclude { get; }

    private AddressSelector(
        IReadOnlyList<AddressObjectId> include,
        IReadOnlyList<AddressObjectId> exclude)
    {
        Include = include;
        Exclude = exclude;
    }

    public static AddressSelector Create(
        IEnumerable<AddressObjectId>? include = null,
        IEnumerable<AddressObjectId>? exclude = null)
    {
        AddressObjectId[] includeIds = (include ?? []).ToArray();
        AddressObjectId[] excludeIds = (exclude ?? []).ToArray();
        EnsureUnique(includeIds, "include");
        EnsureUnique(excludeIds, "exclude");
        return new AddressSelector(includeIds, excludeIds);
    }

    /// <summary>Empty include means Universe(family) before exclusions (Policy Model §17).</summary>
    public bool UsesUniverseInclude => Include.Count == 0;

    private static void EnsureUnique(IReadOnlyList<AddressObjectId> ids, string label)
    {
        HashSet<Guid> seen = [];
        foreach (AddressObjectId id in ids)
        {
            if (!seen.Add(id.Value))
            {
                throw new DomainInvariantException($"Duplicate address object id in selector {label}.");
            }
        }
    }
}

/// <summary>Result of resolving an <see cref="AddressSelector"/>.</summary>
public sealed class AddressSelectorResolveResult
{
    public required IpAddressFamily Family { get; init; }

    public required IReadOnlyList<AddressInterval> Intervals { get; init; }

    /// <summary>Empty result is RULE_UNSATISFIABLE (Policy Model §17).</summary>
    public bool IsUnsatisfiable => Intervals.Count == 0;

    public const string UnsatisfiableCode = "RULE_UNSATISFIABLE";
}

/// <summary>Resolves selectors against a catalog of address objects.</summary>
public static class AddressSelectorResolver
{
    public static AddressSelectorResolveResult Resolve(
        AddressSelector selector,
        IpAddressFamily family,
        IReadOnlyDictionary<AddressObjectId, AddressObject> catalog)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(catalog);

        IReadOnlyList<AddressInterval> include = selector.UsesUniverseInclude
            ? [AddressInterval.Universe(family)]
            : UnionObjects(selector.Include, family, catalog);

        IReadOnlyList<AddressInterval> exclude = selector.Exclude.Count == 0
            ? []
            : UnionObjects(selector.Exclude, family, catalog);

        IReadOnlyList<AddressInterval> result = AddressSetAlgebra.Subtract(include, exclude);
        return new AddressSelectorResolveResult
        {
            Family = family,
            Intervals = result,
        };
    }

    private static IReadOnlyList<AddressInterval> UnionObjects(
        IReadOnlyList<AddressObjectId> ids,
        IpAddressFamily family,
        IReadOnlyDictionary<AddressObjectId, AddressObject> catalog)
    {
        List<AddressInterval> intervals = [];
        foreach (AddressObjectId id in ids)
        {
            if (!catalog.TryGetValue(id, out AddressObject? obj))
            {
                throw new DomainInvariantException($"Address object '{id}' was not found in the catalog.");
            }

            if (obj.Family != family)
            {
                throw new DomainInvariantException(
                    $"Address object '{id}' family {obj.Family} does not match selector family {family}.");
            }

            intervals.AddRange(obj.Intervals);
        }

        return AddressSetAlgebra.Normalize(intervals);
    }
}
