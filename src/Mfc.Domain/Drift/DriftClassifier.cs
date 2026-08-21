namespace Mfc.Domain.Drift;

/// <summary>Pure severity mapping for E2E Spec §33 drift classes (+ N1-07 path-class kinds).</summary>
public static class DriftClassifier
{
    /// <summary>Maps <paramref name="kind"/> to its normative severity.</summary>
    public static DriftSeverity Classify(DriftFindingKind kind)
        => kind switch
        {
            DriftFindingKind.ManagedRuleChanged => DriftSeverity.Critical,
            DriftFindingKind.ManagedRuleReordered => DriftSeverity.Critical,
            DriftFindingKind.ManagedRuleMissing => DriftSeverity.Critical,
            DriftFindingKind.AnchorMissing => DriftSeverity.Critical,
            DriftFindingKind.AnchorDisabled => DriftSeverity.Critical,
            DriftFindingKind.AnchorTargetChanged => DriftSeverity.Critical,
            DriftFindingKind.AnchorPositionChanged => DriftSeverity.Critical,
            DriftFindingKind.ManagementGuardChanged => DriftSeverity.Critical,
            DriftFindingKind.ManagedAddressListChanged => DriftSeverity.Critical,
            DriftFindingKind.InterfaceListMembershipChanged => DriftSeverity.Critical,
            DriftFindingKind.ZoneResolutionChanged => DriftSeverity.Critical,
            DriftFindingKind.RouterOsVersionChanged => DriftSeverity.Critical,
            DriftFindingKind.CapabilityChanged => DriftSeverity.Critical,
            DriftFindingKind.VrrpMembershipConfigChanged => DriftSeverity.Critical,
            DriftFindingKind.NatRawMangleDependencyChanged => DriftSeverity.Critical,
            DriftFindingKind.RoutingConfigurationChanged => DriftSeverity.Critical,
            // Spec §33: Critical/Warning — MVP treats pre-anchor unmanaged as Critical (blocks deploy).
            DriftFindingKind.UnmanagedPreAnchorRule => DriftSeverity.Critical,
            DriftFindingKind.UnmanagedPostAnchorRule => DriftSeverity.Warning,
            DriftFindingKind.VrrpRoleChanged => DriftSeverity.Observation,
            DriftFindingKind.ActiveWanChanged => DriftSeverity.Observation,
            DriftFindingKind.InterfaceRunningStateChanged => DriftSeverity.Observation,
            DriftFindingKind.CountersChanged => DriftSeverity.Ignored,
            DriftFindingKind.ContainerRunningStateChanged => DriftSeverity.Observation,
            DriftFindingKind.VethConfigChanged => DriftSeverity.Critical,
            DriftFindingKind.VlanConfigChanged => DriftSeverity.Critical,
            DriftFindingKind.BridgeMembershipConfigChanged => DriftSeverity.Critical,
            DriftFindingKind.VrfAssignmentConfigChanged => DriftSeverity.Critical,
            DriftFindingKind.ContainerNatExposureConfigChanged => DriftSeverity.Critical,
            DriftFindingKind.HardwarePathConfigChanged => DriftSeverity.Critical,
            DriftFindingKind.VethRunningStateChanged => DriftSeverity.Observation,
            DriftFindingKind.BridgePortStateChanged => DriftSeverity.Observation,
            DriftFindingKind.HardwareOffloadStateChanged => DriftSeverity.Observation,
            _ => throw new DomainInvariantException($"Unknown drift finding kind '{kind}'."),
        };
}
