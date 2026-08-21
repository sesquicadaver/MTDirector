namespace Mfc.Domain.Drift;

/// <summary>Typed drift classes from E2E Spec §33 (+ next-1 / N1-07 path-class extensions).</summary>
public enum DriftFindingKind : byte
{
    ManagedRuleChanged = 1,
    ManagedRuleReordered = 2,
    ManagedRuleMissing = 3,
    AnchorMissing = 4,
    AnchorDisabled = 5,
    AnchorTargetChanged = 6,
    AnchorPositionChanged = 7,
    ManagementGuardChanged = 8,
    ManagedAddressListChanged = 9,
    InterfaceListMembershipChanged = 10,
    ZoneResolutionChanged = 11,
    RouterOsVersionChanged = 12,
    CapabilityChanged = 13,
    VrrpMembershipConfigChanged = 14,
    NatRawMangleDependencyChanged = 15,
    RoutingConfigurationChanged = 16,
    UnmanagedPreAnchorRule = 17,
    UnmanagedPostAnchorRule = 18,
    VrrpRoleChanged = 19,
    ActiveWanChanged = 20,
    InterfaceRunningStateChanged = 21,
    CountersChanged = 22,

    /// <summary>next-1 observation: container running/stopped alone is not configuration drift.</summary>
    ContainerRunningStateChanged = 23,

    /// <summary>next-1 configuration: VETH binding / addresses / gateways changed.</summary>
    VethConfigChanged = 24,

    /// <summary>next-1 configuration: VLAN interface / VLAN-ID / parent changed.</summary>
    VlanConfigChanged = 25,

    /// <summary>next-1 configuration: bridge membership / PVID / tagged-untagged / vlan-filtering.</summary>
    BridgeMembershipConfigChanged = 26,

    /// <summary>next-1 configuration: VRF interface assignment changed.</summary>
    VrfAssignmentConfigChanged = 27,

    /// <summary>next-1 configuration: Apps/container NAT exposure / published port-forward resources.</summary>
    ContainerNatExposureConfigChanged = 28,

    /// <summary>next-1 configuration: L3HW / hardware path configuration changed.</summary>
    HardwarePathConfigChanged = 29,

    /// <summary>next-1 observation: VETH running state (config hashes may still match).</summary>
    VethRunningStateChanged = 30,

    /// <summary>next-1 observation: current bridge-port state.</summary>
    BridgePortStateChanged = 31,

    /// <summary>next-1 observation: current hardware-offload state (not L3HW config).</summary>
    HardwareOffloadStateChanged = 32,
}
