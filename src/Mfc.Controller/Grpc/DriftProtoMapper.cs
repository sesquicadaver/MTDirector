using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;
using DomainFindingKind = Mfc.Domain.Drift.DriftFindingKind;
using DomainOutcome = Mfc.Domain.Drift.DriftOutcome;
using DomainSeverity = Mfc.Domain.Drift.DriftSeverity;
using ProtoDriftEvent = Mfc.Contracts.Mfc.V1.DriftEvent;
using ProtoDriftFinding = Mfc.Contracts.Mfc.V1.DriftFinding;
using ProtoFindingKind = Mfc.Contracts.Mfc.V1.DriftFindingKind;
using ProtoOutcome = Mfc.Contracts.Mfc.V1.DriftOutcome;
using ProtoSeverity = Mfc.Contracts.Mfc.V1.DriftSeverity;

namespace Mfc.Controller.Grpc;

/// <summary>Maps Application drift views to Contracts proto messages (M6-04).</summary>
internal static class DriftProtoMapper
{
    public static ProtoDriftEvent ToProto(DriftEventView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ProtoDriftEvent message = new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            DeviceId = ProtoUuid.FromGuid(view.DeviceId),
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            Outcome = ToProto(view.Outcome),
            ConfigurationDriftPresent = view.ConfigurationDriftPresent,
            BlocksDeployment = view.BlocksDeployment,
            CreatedAt = Timestamp.FromDateTimeOffset(view.CreatedAtUtc),
            Immutable = view.Immutable,
        };
        if (TryHexToSha256(view.BaselineCommittedHashHex, out Sha256? baseline))
        {
            message.BaselineCommittedHash = baseline;
        }

        if (TryHexToSha256(view.ActualManagedResourceHashHex, out Sha256? actual))
        {
            message.ActualManagedResourceHash = actual;
        }

        if (TryHexToSha256(view.DesiredArtifactHashIgnoredForBaselineHex, out Sha256? desired))
        {
            message.DesiredArtifactHashIgnoredForBaseline = desired;
        }

        if (!string.IsNullOrWhiteSpace(view.SemanticDiffCanonical))
        {
            message.SemanticDiffCanonical = view.SemanticDiffCanonical;
        }

        if (TryHexToSha256(view.SemanticDiffHashHex, out Sha256? semanticHash))
        {
            message.SemanticDiffHash = semanticHash;
        }

        message.Findings.AddRange(view.Findings.Select(ToProto));
        return message;
    }

    public static ProtoDriftFinding ToProto(DriftFindingView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ProtoDriftFinding message = new()
        {
            Kind = ToProto(view.Kind),
            Severity = ToProto(view.Severity),
        };
        if (!string.IsNullOrWhiteSpace(view.Detail))
        {
            message.Detail = view.Detail;
        }

        return message;
    }

    public static ProtoOutcome ToProto(DomainOutcome outcome) => outcome switch
    {
        DomainOutcome.NoDrift => ProtoOutcome.NoDrift,
        DomainOutcome.ObservationOnly => ProtoOutcome.ObservationOnly,
        DomainOutcome.WarningDrift => ProtoOutcome.WarningDrift,
        DomainOutcome.CriticalDrift => ProtoOutcome.CriticalDrift,
        DomainOutcome.PendingDeploymentNotDrift => ProtoOutcome.PendingDeploymentNotDrift,
        _ => ProtoOutcome.Unspecified,
    };

    public static ProtoSeverity ToProto(DomainSeverity severity) => severity switch
    {
        DomainSeverity.Critical => ProtoSeverity.Critical,
        DomainSeverity.Warning => ProtoSeverity.Warning,
        DomainSeverity.Observation => ProtoSeverity.Observation,
        DomainSeverity.Ignored => ProtoSeverity.Ignored,
        _ => ProtoSeverity.Unspecified,
    };

    public static ProtoFindingKind ToProto(DomainFindingKind kind) => kind switch
    {
        DomainFindingKind.ManagedRuleChanged => ProtoFindingKind.ManagedRuleChanged,
        DomainFindingKind.ManagedRuleReordered => ProtoFindingKind.ManagedRuleReordered,
        DomainFindingKind.ManagedRuleMissing => ProtoFindingKind.ManagedRuleMissing,
        DomainFindingKind.AnchorMissing => ProtoFindingKind.AnchorMissing,
        DomainFindingKind.AnchorDisabled => ProtoFindingKind.AnchorDisabled,
        DomainFindingKind.AnchorTargetChanged => ProtoFindingKind.AnchorTargetChanged,
        DomainFindingKind.AnchorPositionChanged => ProtoFindingKind.AnchorPositionChanged,
        DomainFindingKind.ManagementGuardChanged => ProtoFindingKind.ManagementGuardChanged,
        DomainFindingKind.ManagedAddressListChanged => ProtoFindingKind.ManagedAddressListChanged,
        DomainFindingKind.InterfaceListMembershipChanged => ProtoFindingKind.InterfaceListMembershipChanged,
        DomainFindingKind.ZoneResolutionChanged => ProtoFindingKind.ZoneResolutionChanged,
        DomainFindingKind.RouterOsVersionChanged => ProtoFindingKind.RouterosVersionChanged,
        DomainFindingKind.CapabilityChanged => ProtoFindingKind.CapabilityChanged,
        DomainFindingKind.VrrpMembershipConfigChanged => ProtoFindingKind.VrrpMembershipConfigChanged,
        DomainFindingKind.NatRawMangleDependencyChanged => ProtoFindingKind.NatRawMangleDependencyChanged,
        DomainFindingKind.RoutingConfigurationChanged => ProtoFindingKind.RoutingConfigurationChanged,
        DomainFindingKind.UnmanagedPreAnchorRule => ProtoFindingKind.UnmanagedPreAnchorRule,
        DomainFindingKind.UnmanagedPostAnchorRule => ProtoFindingKind.UnmanagedPostAnchorRule,
        DomainFindingKind.VrrpRoleChanged => ProtoFindingKind.VrrpRoleChanged,
        DomainFindingKind.ActiveWanChanged => ProtoFindingKind.ActiveWanChanged,
        DomainFindingKind.InterfaceRunningStateChanged => ProtoFindingKind.InterfaceRunningStateChanged,
        DomainFindingKind.CountersChanged => ProtoFindingKind.CountersChanged,
        DomainFindingKind.ContainerRunningStateChanged => ProtoFindingKind.ContainerRunningStateChanged,
        DomainFindingKind.VethConfigChanged => ProtoFindingKind.VethConfigChanged,
        DomainFindingKind.VlanConfigChanged => ProtoFindingKind.VlanConfigChanged,
        DomainFindingKind.BridgeMembershipConfigChanged => ProtoFindingKind.BridgeMembershipConfigChanged,
        DomainFindingKind.VrfAssignmentConfigChanged => ProtoFindingKind.VrfAssignmentConfigChanged,
        DomainFindingKind.ContainerNatExposureConfigChanged => ProtoFindingKind.ContainerNatExposureConfigChanged,
        DomainFindingKind.HardwarePathConfigChanged => ProtoFindingKind.HardwarePathConfigChanged,
        DomainFindingKind.VethRunningStateChanged => ProtoFindingKind.VethRunningStateChanged,
        DomainFindingKind.BridgePortStateChanged => ProtoFindingKind.BridgePortStateChanged,
        DomainFindingKind.HardwareOffloadStateChanged => ProtoFindingKind.HardwareOffloadStateChanged,
        _ => ProtoFindingKind.Unspecified,
    };

    private static bool TryHexToSha256(string? hex, out Sha256? sha)
    {
        sha = null;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        try
        {
            byte[] bytes = Convert.FromHexString(hex);
            if (bytes.Length != 32)
            {
                return false;
            }

            sha = new Sha256 { Value = ByteString.CopyFrom(bytes) };
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
