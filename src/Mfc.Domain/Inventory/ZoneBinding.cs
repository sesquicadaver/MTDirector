using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Inventory;

/// <summary>
/// Zone-to-interface binding for policy compilation. Not a persisted aggregate in M1 (Vertical Slice §31).
/// </summary>
public sealed class ZoneBinding
{
    private readonly List<string> _bindingValues;
    private readonly List<string> _resolvedMembers;

    public ZoneBindingId Id { get; }

    public NodeId NodeId { get; }

    public NonEmptyName ZoneKey { get; }

    public ZoneAddressFamily Family { get; }

    public ZoneBindingType BindingType { get; }

    public IReadOnlyList<string> BindingValues => _bindingValues;

    public IReadOnlyList<string> ResolvedMembers => _resolvedMembers;

    public Hash256 DependencyHash { get; private set; }

    private ZoneBinding(
        ZoneBindingId id,
        NodeId nodeId,
        NonEmptyName zoneKey,
        ZoneAddressFamily family,
        ZoneBindingType bindingType,
        List<string> bindingValues,
        List<string> resolvedMembers,
        Hash256 dependencyHash)
    {
        Id = id;
        NodeId = nodeId;
        ZoneKey = zoneKey;
        Family = family;
        BindingType = bindingType;
        _bindingValues = bindingValues;
        _resolvedMembers = resolvedMembers;
        DependencyHash = dependencyHash;
    }

    public static ZoneBinding Create(
        NodeId nodeId,
        NonEmptyName zoneKey,
        ZoneAddressFamily family,
        ZoneBindingType bindingType,
        IEnumerable<string> bindingValues,
        IEnumerable<string> resolvedMembers,
        Hash256 dependencyHash)
    {
        ArgumentNullException.ThrowIfNull(zoneKey);
        ArgumentNullException.ThrowIfNull(bindingValues);
        ArgumentNullException.ThrowIfNull(resolvedMembers);
        ArgumentNullException.ThrowIfNull(dependencyHash);

        List<string> values = NormalizeNames(bindingValues, "binding_value");
        List<string> resolved = NormalizeNames(resolvedMembers, "resolved_members");
        if (values.Count == 0)
        {
            throw new DomainInvariantException("ZoneBinding requires at least one binding_value.");
        }

        return new ZoneBinding(
            ZoneBindingId.New(),
            nodeId,
            zoneKey,
            family,
            bindingType,
            values,
            resolved,
            dependencyHash);
    }

    public void ReplaceResolvedMembers(IEnumerable<string> resolvedMembers, Hash256 dependencyHash)
    {
        ArgumentNullException.ThrowIfNull(resolvedMembers);
        ArgumentNullException.ThrowIfNull(dependencyHash);
        _resolvedMembers.Clear();
        _resolvedMembers.AddRange(NormalizeNames(resolvedMembers, "resolved_members"));
        DependencyHash = dependencyHash;
    }

    private static List<string> NormalizeNames(IEnumerable<string> values, string fieldName)
    {
        List<string> result = [];
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new DomainInvariantException($"{fieldName} entries must be non-empty.");
            }

            result.Add(value.Trim());
        }

        return result;
    }
}
