namespace Mfc.Domain.Routing;

/// <summary>
/// Pure classifier of RouterOS property names / hash-material key suffixes into
/// configuration vs observation (M7.1 Spec §2, §14). Deterministic and side-effect free.
/// </summary>
public static class RoutingAssurancePropertyClassifier
{
    private static readonly HashSet<string> ObservationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "inactive",
        "invalid",
        "running",
        "gateway-status",
        "immediate-gw",
        "immediate-gateway",
        "hw-offloaded",
        "hardware-offloaded",
        "dynamic",
        "current",
        "selected",
        "ecmp",
        "reachable",
        "unreachable",
    };

    /// <summary>
    /// Classifies a bare RouterOS property name (e.g. <c>active</c>, <c>distance</c>).
    /// Unknown names default to <see cref="RoutingAssurancePropertyKind.Configuration"/>.
    /// </summary>
    public static RoutingAssurancePropertyKind ClassifyPropertyName(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        string name = propertyName.Trim();
        if (name.StartsWith('.'))
        {
            // .id and similar system ids are raw identity, not operational FIB state.
            return RoutingAssurancePropertyKind.Configuration;
        }

        return ObservationNames.Contains(name)
            ? RoutingAssurancePropertyKind.Observation
            : RoutingAssurancePropertyKind.Configuration;
    }

    /// <summary>
    /// Classifies a hash-material key such as <c>route.main:0.0.0.0/0:1.1.1.1.active</c>
    /// by its final dotted segment.
    /// </summary>
    public static RoutingAssurancePropertyKind ClassifyMaterialKey(string materialKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialKey);
        string key = materialKey.Trim();
        int lastDot = key.LastIndexOf('.');
        string leaf = lastDot >= 0 && lastDot < key.Length - 1 ? key[(lastDot + 1)..] : key;
        return ClassifyPropertyName(leaf);
    }
}
