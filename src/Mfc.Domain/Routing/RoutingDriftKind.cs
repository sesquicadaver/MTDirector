namespace Mfc.Domain.Routing;

/// <summary>
/// Routing drift categories aligned with M7.1 Spec §14.
/// Configuration drift ≠ operational routing change.
/// </summary>
public enum RoutingDriftKind : byte
{
    // ── Configuration drift (§14) ────────────────────────────────────────────

    /// <summary>Routing table created, removed, disabled, or FIB flag changed.</summary>
    RoutingTableChanged = 1,

    /// <summary>Routing decision order or check-gateway settings changed.</summary>
    RoutingSettingsChanged = 2,

    /// <summary>Routing rule changed or reordered.</summary>
    RoutingRuleChanged = 3,

    /// <summary>VRF definition or interface binding changed.</summary>
    VrfBindingChanged = 4,

    /// <summary>Static route distance, scope, target-scope, check-gateway, or suppress-hw-offload changed.</summary>
    StaticRouteChanged = 5,

    /// <summary>Route filter rule or select-rule changed or reordered.</summary>
    RouteFilterChanged = 6,

    /// <summary>NAT/RAW/Mangle routing dependency or IP forwarding settings changed.</summary>
    FirewallRoutingDependencyChanged = 7,

    // ── Operational routing change (§14) ─────────────────────────────────────

    /// <summary>Active route selection changed.</summary>
    ActiveRouteChanged = 20,

    /// <summary>Gateway became unreachable or reachability status changed adversely.</summary>
    GatewayUnreachable = 21,

    /// <summary>ECMP member or immediate gateway changed.</summary>
    EcmpMemberChanged = 22,

    /// <summary>Dynamic best path or route type changed.</summary>
    DynamicBestPathChanged = 23,

    /// <summary>Protocol session / dynamic route observation changed.</summary>
    ProtocolSessionChanged = 24,

    /// <summary>Route moved between CPU and hardware offload.</summary>
    RouteExecutionPathChanged = 25,

    /// <summary>Default WAN / default-route gateway changed.</summary>
    DefaultWanChanged = 26,
}
