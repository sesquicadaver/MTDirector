namespace Mfc.Domain.Routing;

/// <summary>Finding codes for <see cref="RoutingDriftAnalyzer"/> (M7.1 Spec §14).</summary>
public static class RoutingDriftCodes
{
    /// <summary>Umbrella finding when configuration hash material changed.</summary>
    public const string ConfigurationDrift = "ROUTING_CONFIGURATION_DRIFT";

    /// <summary>Umbrella finding when operational hash material changed.</summary>
    public const string OperationalChange = "ROUTING_OPERATIONAL_CHANGE";

    public const string RoutingTableChanged = "ROUTING_TABLE_CHANGED";

    public const string RoutingSettingsChanged = "ROUTING_SETTINGS_CHANGED";

    public const string RoutingRuleChanged = "ROUTING_RULE_CHANGED";

    public const string VrfBindingChanged = "ROUTING_VRF_BINDING_CHANGED";

    public const string StaticRouteChanged = "ROUTING_STATIC_ROUTE_CHANGED";

    public const string RouteFilterChanged = "ROUTING_ROUTE_FILTER_CHANGED";

    public const string ProtocolConfigurationChanged = "ROUTING_PROTOCOL_CONFIGURATION_CHANGED";

    public const string ActiveRouteChanged = "ROUTING_ACTIVE_ROUTE_CHANGED";

    public const string GatewayUnreachable = "ROUTING_GATEWAY_UNREACHABLE";

    public const string EcmpMemberChanged = "ROUTING_ECMP_MEMBER_CHANGED";

    public const string DynamicBestPathChanged = "ROUTING_DYNAMIC_BEST_PATH_CHANGED";

    public const string ProtocolSessionChanged = "ROUTING_PROTOCOL_SESSION_CHANGED";

    public const string RouteExecutionPathChanged = "ROUTING_EXECUTION_PATH_CHANGED";

    public const string DefaultWanChanged = "ROUTING_DEFAULT_WAN_CHANGED";

    /// <summary>Maps <see cref="RoutingDriftKind"/> to its specific finding code.</summary>
    public static string CodeForKind(RoutingDriftKind kind)
        => kind switch
        {
            RoutingDriftKind.RoutingTableChanged => RoutingTableChanged,
            RoutingDriftKind.RoutingSettingsChanged => RoutingSettingsChanged,
            RoutingDriftKind.RoutingRuleChanged => RoutingRuleChanged,
            RoutingDriftKind.VrfBindingChanged => VrfBindingChanged,
            RoutingDriftKind.StaticRouteChanged => StaticRouteChanged,
            RoutingDriftKind.RouteFilterChanged => RouteFilterChanged,
            RoutingDriftKind.FirewallRoutingDependencyChanged => ProtocolConfigurationChanged,
            RoutingDriftKind.ActiveRouteChanged => ActiveRouteChanged,
            RoutingDriftKind.GatewayUnreachable => GatewayUnreachable,
            RoutingDriftKind.EcmpMemberChanged => EcmpMemberChanged,
            RoutingDriftKind.DynamicBestPathChanged => DynamicBestPathChanged,
            RoutingDriftKind.ProtocolSessionChanged => ProtocolSessionChanged,
            RoutingDriftKind.RouteExecutionPathChanged => RouteExecutionPathChanged,
            RoutingDriftKind.DefaultWanChanged => DefaultWanChanged,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown routing drift kind."),
        };
}
