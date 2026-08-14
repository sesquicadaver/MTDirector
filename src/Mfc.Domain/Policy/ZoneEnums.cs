namespace Mfc.Domain.Policy;

/// <summary>Desired Node zone binding kinds (Policy Model §21).</summary>
public enum NodeZoneBindingKind : byte
{
    InterfaceList = 0,
    SingleInterface = 1,
    ExplicitInterfaceSet = 2,
}

/// <summary>Typed resolve / observation blockers for zone bindings (M2-05 / N1-05).</summary>
public static class ZoneResolveBlockerCodes
{
    public const string EmptyResolvedSet = "ZONE_EMPTY_RESOLVED_SET";
    public const string MissingInterface = "ZONE_MISSING_INTERFACE";
    public const string DynamicInterface = "ZONE_DYNAMIC_INTERFACE";
    public const string ObservationUnavailable = "ZONE_OBSERVATION_UNAVAILABLE";
    public const string InterfaceListCycle = "ZONE_INTERFACE_LIST_CYCLE";
    public const string MissingInterfaceList = "ZONE_MISSING_INTERFACE_LIST";
    public const string MissingContainer = "ZONE_MISSING_CONTAINER";
    public const string MissingApp = "ZONE_MISSING_APP";
    public const string ContainerVethUnresolved = "ZONE_CONTAINER_VETH_UNRESOLVED";
    public const string AppVethUnresolved = "ZONE_APP_VETH_UNRESOLVED";
    public const string SharedVeth = "ZONE_SHARED_VETH";
    public const string MarkerNotAllowedOnInterfaceList = "ZONE_MARKER_NOT_ALLOWED_ON_INTERFACE_LIST";
}

/// <summary>Single typed blocker from zone resolve.</summary>
public sealed class ZoneResolveBlocker
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}
