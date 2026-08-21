namespace Mfc.Domain.Routing;

/// <summary>
/// Classification of routing-assurance properties (M7.1 Spec §2 / §14).
/// Configuration drift ≠ operational change.
/// </summary>
public enum RoutingAssurancePropertyKind : byte
{
    /// <summary>Authoritative configured state (tables, rules, VRF, static routes, filters, settings).</summary>
    Configuration = 1,

    /// <summary>Runtime observation (active/inactive, gateway reachability, HW offload, defaults).</summary>
    Observation = 2,
}
