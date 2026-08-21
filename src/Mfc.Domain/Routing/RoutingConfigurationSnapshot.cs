namespace Mfc.Domain.Routing;

/// <summary>
/// Structured routing configuration snapshot (M7.1 Spec §2 Configuration).
/// Hash material excludes observation-only fields (active, gateway-status, immediate-gw, …).
/// </summary>
public sealed class RoutingConfigurationSnapshot
{
    public IReadOnlyList<RoutingTableFact> Tables { get; }

    public RoutingSettingsFact Settings { get; }

    public IReadOnlyList<RoutingRuleFact> Rules { get; }

    public IReadOnlyList<VrfDefinitionFact> Vrfs { get; }

    public IReadOnlyList<StaticRouteConfigFact> StaticRoutes { get; }

    public IReadOnlyList<RouteFilterRuleFact> FilterRules { get; }

    public IReadOnlyList<RouteFilterSelectRuleFact> FilterSelectRules { get; }

    /// <summary>Deterministic key/value material used for <see cref="RoutingAssuranceHashContract.HashConfiguration"/>.</summary>
    public IReadOnlyDictionary<string, string> HashMaterial { get; }

    public RoutingConfigurationSnapshot(
        IReadOnlyList<RoutingTableFact> tables,
        RoutingSettingsFact settings,
        IReadOnlyList<RoutingRuleFact> rules,
        IReadOnlyList<VrfDefinitionFact> vrfs,
        IReadOnlyList<StaticRouteConfigFact> staticRoutes,
        IReadOnlyList<RouteFilterRuleFact> filterRules,
        IReadOnlyList<RouteFilterSelectRuleFact> filterSelectRules,
        IReadOnlyDictionary<string, string> hashMaterial)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(vrfs);
        ArgumentNullException.ThrowIfNull(staticRoutes);
        ArgumentNullException.ThrowIfNull(filterRules);
        ArgumentNullException.ThrowIfNull(filterSelectRules);
        ArgumentNullException.ThrowIfNull(hashMaterial);
        Tables = tables;
        Settings = settings;
        Rules = rules;
        Vrfs = vrfs;
        StaticRoutes = staticRoutes;
        FilterRules = filterRules;
        FilterSelectRules = filterSelectRules;
        HashMaterial = hashMaterial;
    }

    /// <summary>Empty configuration shell with empty hash material.</summary>
    public static RoutingConfigurationSnapshot Empty { get; } = new(
        [],
        RoutingSettingsFact.Empty,
        [],
        [],
        [],
        [],
        [],
        new Dictionary<string, string>(StringComparer.Ordinal));
}

/// <summary>Configured routing table entry.</summary>
public sealed class RoutingTableFact
{
    public required string? Name { get; init; }

    public required string? Fib { get; init; }

    public required string? Disabled { get; init; }
}

/// <summary>Routing decision-order / check-gateway settings.</summary>
public sealed class RoutingSettingsFact
{
    public required string? PolicyRules { get; init; }

    public required string? CheckGatewayPingCount { get; init; }

    public required string? CheckGatewayPingInterval { get; init; }

    public required string? CheckGatewayPingTimeout { get; init; }

    public required string? ConnectedInChain { get; init; }

    public required string? DynamicInChain { get; init; }

    public required string? SingleProcess { get; init; }

    public static RoutingSettingsFact Empty { get; } = new()
    {
        PolicyRules = null,
        CheckGatewayPingCount = null,
        CheckGatewayPingInterval = null,
        CheckGatewayPingTimeout = null,
        ConnectedInChain = null,
        DynamicInChain = null,
        SingleProcess = null,
    };
}

/// <summary>Configured policy routing rule (non-dynamic).</summary>
public sealed class RoutingRuleFact
{
    public required int EffectiveOrdinal { get; init; }

    public required string? Action { get; init; }

    public required string? SrcAddress { get; init; }

    public required string? DstAddress { get; init; }

    public required string? RoutingMark { get; init; }

    public required string? Table { get; init; }

    public required string? Disabled { get; init; }
}

/// <summary>VRF definition and interface bindings.</summary>
public sealed class VrfDefinitionFact
{
    public required string? Name { get; init; }

    public required string? Interfaces { get; init; }

    public required string? Disabled { get; init; }
}

/// <summary>Static route configuration (observations stripped).</summary>
public sealed class StaticRouteConfigFact
{
    public required string Family { get; init; }

    public required string? DstAddress { get; init; }

    public required string? Gateway { get; init; }

    public required string? RoutingTable { get; init; }

    public required int? Distance { get; init; }

    public required int? Scope { get; init; }

    public required int? TargetScope { get; init; }

    public required string? PrefSrc { get; init; }

    public required string? CheckGateway { get; init; }

    public required string? Disabled { get; init; }
}

/// <summary>Route filter rule configuration.</summary>
public sealed class RouteFilterRuleFact
{
    public required int EffectiveOrdinal { get; init; }

    public required string? Chain { get; init; }

    public required string? Rule { get; init; }

    public required string? Disabled { get; init; }
}

/// <summary>Route filter select-rule configuration.</summary>
public sealed class RouteFilterSelectRuleFact
{
    public required int EffectiveOrdinal { get; init; }

    public required string? Chain { get; init; }

    public required string? Rule { get; init; }

    public required string? Disabled { get; init; }
}
