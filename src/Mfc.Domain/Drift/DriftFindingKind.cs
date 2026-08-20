namespace Mfc.Domain.Drift;

/// <summary>Typed drift classes from E2E Spec §33.</summary>
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
}
