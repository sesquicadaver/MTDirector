using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Topology;

/// <summary>Severity of a topology validation finding (M1-18).</summary>
public enum TopologyFindingSeverity : byte
{
    Finding = 0,
    Blocker = 1,
}

/// <summary>
/// Observed uplink evidence from routing/NAT/Mangle — never interface count alone (M1-18 AC#7–8).
/// </summary>
public enum ObservedUplinkEvidence : byte
{
    None = 0,
    SingleDefaultRoute = 1,
    FailoverDistanceRoutes = 2,
    BalancedPccOrEcmp = 3,
    Mixed = 4,

    /// <summary>Evidence insufficient to classify — must surface a finding, not an assumption.</summary>
    Insufficient = 5,
}

/// <summary>Board role used for SWITCH transit-firewall gating.</summary>
public enum ObservedBoardRole : byte
{
    Router = 0,
    Switch = 1,
    Unknown = 2,
}

/// <summary>One observed VRRP instance on a device (family + VRID scoped).</summary>
public sealed record ObservedVrrpInstance
{
    public required IpAddressFamily Family { get; init; }

    public required byte Vrid { get; init; }

    public required string InterfaceKey { get; init; }

    public required VrrpMemberObservedState ObservedState { get; init; }

    public required string RouterOsVersion { get; init; }
}

/// <summary>
/// Explicit per-device topology facts supplied by the caller.
/// Controller never auto-scans the network to obtain these (M1-18 AC#1).
/// </summary>
public sealed record DeviceTopologyFacts
{
    public required DeviceId DeviceId { get; init; }

    public required string RouterOsVersion { get; init; }

    public required ObservedBoardRole BoardRole { get; init; }

    /// <summary>False when device is not bound to the validated node.</summary>
    public required bool IsExplicitlyBoundToNode { get; init; }

    public required IReadOnlyList<ObservedVrrpInstance> VrrpInstances { get; init; }

    public required ObservedUplinkEvidence UplinkEvidence { get; init; }

    /// <summary>Interface count is never used alone to classify uplink mode; informational only.</summary>
    public required int ObservedUplinkInterfaceCount { get; init; }

    /// <summary>SWITCH nodes must keep this false — no transit firewall capability.</summary>
    public required bool GrantsTransitFirewallCapability { get; init; }

    public CapabilityHash? CapabilityHash { get; init; }
}

public sealed record TopologyValidationFinding
{
    public const string DeviceNotBoundToNode = "DEVICE_NOT_BOUND_TO_NODE";
    public const string RouterCardinalityViolation = "ROUTER_CARDINALITY_VIOLATION";
    public const string SwitchCardinalityViolation = "SWITCH_CARDINALITY_VIOLATION";
    public const string VrrpCardinalityViolation = "VRRP_CARDINALITY_VIOLATION";
    public const string VrrpGroupMembershipMismatch = "VRRP_GROUP_MEMBERSHIP_MISMATCH";
    public const string VrrpVersionMismatch = "VRRP_VERSION_MISMATCH";
    public const string VrrpSplitMaster = "VRRP_SPLIT_MASTER";
    public const string UplinkModeEvidenceMismatch = "UPLINK_MODE_EVIDENCE_MISMATCH";
    public const string UplinkModeUncertain = "UPLINK_MODE_UNCERTAIN";
    public const string SwitchTransitFirewallForbidden = "SWITCH_TRANSIT_FIREWALL_FORBIDDEN";
    public const string BoardRoleUncertain = "BOARD_ROLE_UNCERTAIN";
    public const string FactsDeviceUnknown = "FACTS_DEVICE_UNKNOWN";

    public required string Code { get; init; }

    public required string Message { get; init; }

    public required TopologyFindingSeverity Severity { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Deterministic topology validation result for one node.</summary>
public sealed class NodeTopologyValidationResult
{
    public required NodeId NodeId { get; init; }

    public required bool IsValid { get; init; }

    public required IReadOnlyList<TopologyValidationFinding> Findings { get; init; }

    public required ObservedUplinkEvidence EffectiveUplinkEvidence { get; init; }

    /// <summary>True when validation was skipped because capability cache still matches.</summary>
    public required bool UsedCapabilityCache { get; init; }
}
