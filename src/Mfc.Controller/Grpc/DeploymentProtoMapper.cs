using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mfc.Application.Deployment;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using AppDeviceView = Mfc.Application.Deployment.DeploymentDevicePlanView;
using AppDiffKind = Mfc.Application.Deployment.DeploymentSemanticDiffKind;
using AppProbeView = Mfc.Application.Deployment.DeploymentProbeView;
using DomainAction = Mfc.Domain.Deployment.DeploymentRecoveryAction;
using DomainPathKind = Mfc.Domain.Policy.PacketPathKind;
using DomainProbeKind = Mfc.Domain.Deployment.DeploymentProbeKind;
using DomainState = Mfc.Domain.Deployment.DeploymentOperationState;
using ProtoAction = Mfc.Contracts.Mfc.V1.DeploymentRecoveryAction;
using ProtoDeviceView = Mfc.Contracts.Mfc.V1.DeploymentDevicePlanView;
using ProtoDiffKind = Mfc.Contracts.Mfc.V1.DeploymentSemanticDiffKind;
using ProtoPathKind = Mfc.Contracts.Mfc.V1.DeploymentPacketPathKind;
using ProtoProbeKind = Mfc.Contracts.Mfc.V1.DeploymentProbeKind;
using ProtoProbeView = Mfc.Contracts.Mfc.V1.DeploymentProbeView;
using ProtoState = Mfc.Contracts.Mfc.V1.DeploymentOperationState;

namespace Mfc.Controller.Grpc;

/// <summary>Maps deployment application views onto Contracts wire types (M4-12).</summary>
public static class DeploymentProtoMapper
{
    public static bool IsTerminal(ProtoState state)
        => state is ProtoState.Committed
            or ProtoState.RolledBack
            or ProtoState.Blocked
            or ProtoState.NoChanges
            or ProtoState.Canceled
            or ProtoState.Failed
            or ProtoState.RecoveryRequired;

    public static ProtoState ToProto(DomainState state)
        => state switch
        {
            DomainState.Created => ProtoState.Created,
            DomainState.Prechecking => ProtoState.Prechecking,
            DomainState.Staging => ProtoState.Staging,
            DomainState.Staged => ProtoState.Staged,
            DomainState.ArmingWatchdog => ProtoState.ArmingWatchdog,
            DomainState.WatchdogArmed => ProtoState.WatchdogArmed,
            DomainState.Activating => ProtoState.Activating,
            DomainState.Verifying => ProtoState.Verifying,
            DomainState.DisarmingWatchdog => ProtoState.DisarmingWatchdog,
            DomainState.Committed => ProtoState.Committed,
            DomainState.RollbackPending => ProtoState.RollbackPending,
            DomainState.RollingBack => ProtoState.RollingBack,
            DomainState.RolledBack => ProtoState.RolledBack,
            DomainState.Blocked => ProtoState.Blocked,
            DomainState.NoChanges => ProtoState.NoChanges,
            DomainState.Canceled => ProtoState.Canceled,
            DomainState.Failed => ProtoState.Failed,
            DomainState.RecoveryRequired => ProtoState.RecoveryRequired,
            _ => ProtoState.Unspecified,
        };

    public static ProtoAction ToProto(DomainAction action)
        => action switch
        {
            DomainAction.MarkFailedOrCanceled => ProtoAction.MarkFailedOrCanceled,
            DomainAction.ControllerRollback => ProtoAction.ControllerRollback,
            DomainAction.RecognizeWatchdogRollback => ProtoAction.RecognizeWatchdogRollback,
            DomainAction.RecoveryRequired => ProtoAction.RecoveryRequired,
            DomainAction.KeepCommitted => ProtoAction.KeepCommitted,
            _ => ProtoAction.Unspecified,
        };

    public static DeploymentPlanSummary ToProto(DeploymentPlanSummaryView view)
    {
        DeploymentPlanSummary message = new()
        {
            PlanId = ProtoUuid.FromGuid(view.PlanId),
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            PlanHash = ToSha256(view.PlanHash),
            ExpiresAt = Timestamp.FromDateTimeOffset(view.ExpiresAtUtc),
        };
        message.SemanticDiffEntries.AddRange(view.SemanticDiffEntries);
        foreach (DeploymentSemanticDiffEntryView entry in view.SemanticDiff)
        {
            DeploymentSemanticDiffEntry row = new()
            {
                Kind = ToProto(entry.Kind),
                Path = entry.Path,
                Before = entry.Before,
                After = entry.After,
                HashDelta = entry.HashDelta,
            };
            if (entry.DeviceId is Guid deviceId)
            {
                row.DeviceId = ProtoUuid.FromGuid(deviceId);
            }

            message.SemanticDiff.Add(row);
        }

        foreach (Guid deviceId in view.ActivationOrderDeviceIds)
        {
            message.ActivationOrderDeviceIds.Add(ProtoUuid.FromGuid(deviceId));
        }

        foreach (Guid deviceId in view.RollbackOrderDeviceIds)
        {
            message.RollbackOrderDeviceIds.Add(ProtoUuid.FromGuid(deviceId));
        }

        foreach (AppDeviceView device in view.Devices)
        {
            ProtoDeviceView row = new()
            {
                DeviceId = ProtoUuid.FromGuid(device.DeviceId),
                OldArtifactHash = ToSha256(device.OldArtifactHash),
                NewArtifactHash = ToSha256(device.NewArtifactHash),
                WatchdogTtlSeconds = device.WatchdogTtlSeconds,
            };
            row.ActivationOrderMarkers.AddRange(device.ActivationOrderMarkers);
            row.RollbackOrderMarkers.AddRange(device.RollbackOrderMarkers);
            foreach (AppProbeView probe in device.Probes)
            {
                ProtoProbeView probeRow = new()
                {
                    Kind = ToProto(probe.Kind),
                    Destination = probe.Destination,
                    TimeoutMilliseconds = (uint)probe.TimeoutMilliseconds,
                };
                if (!string.IsNullOrWhiteSpace(probe.SourceAddress))
                {
                    probeRow.SourceAddress = probe.SourceAddress;
                }

                if (!string.IsNullOrWhiteSpace(probe.RoutingTable))
                {
                    probeRow.RoutingTable = probe.RoutingTable;
                }

                if (!string.IsNullOrWhiteSpace(probe.Interface))
                {
                    probeRow.Interface = probe.Interface;
                }

                row.Probes.Add(probeRow);
            }

            message.Devices.Add(row);
        }

        return message;
    }

    public static DeploymentOperationSummary ToProto(DeploymentOperationSummaryView view)
    {
        DeploymentOperationSummary message = new()
        {
            OperationId = ProtoUuid.FromGuid(view.OperationId),
            PlanId = ProtoUuid.FromGuid(view.PlanId),
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            State = ToProto(view.State),
        };
        if (!string.IsNullOrWhiteSpace(view.ErrorCode))
        {
            message.ErrorCode = view.ErrorCode;
        }

        message.Timeline.AddRange(view.Timeline);
        return message;
    }

    public static DeploymentRecoveryStatus ToProto(DeploymentRecoveryStatusView view)
    {
        DeploymentRecoveryStatus message = new()
        {
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            OperationState = ToProto(view.OperationState),
            Action = ToProto(view.Action),
        };
        if (view.OperationId is Guid operationId)
        {
            message.OperationId = ProtoUuid.FromGuid(operationId);
        }

        if (!string.IsNullOrWhiteSpace(view.ErrorCode))
        {
            message.ErrorCode = view.ErrorCode;
        }

        message.DeviceStates.AddRange(view.DeviceStates);
        return message;
    }

    public static DeviceDeploymentPlan ToDevicePlan(DeploymentDevicePlanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        List<AnchorKey> activation = [];
        foreach (string marker in input.AnchorActivationOrderMarkers)
        {
            if (!AnchorKey.TryParse(marker, out AnchorKey key))
            {
                throw new DomainInvariantException($"Invalid anchor activation marker '{marker}'.");
            }

            activation.Add(key);
        }

        List<AnchorTarget> oldTargets = input.OldAnchorTargets.Select(ToAnchorTarget).ToList();
        List<AnchorTarget> newTargets = input.NewAnchorTargets.Select(ToAnchorTarget).ToList();
        List<Hash256> transitions = input.TransitionStateHashes.Select(ToHash).ToList();
        List<DeploymentProbe> probes = input.Probes.Select(ToProbe).ToList();
        TimeSpan? ttl = input.RollbackTtlSeconds > 0
            ? TimeSpan.FromSeconds(input.RollbackTtlSeconds)
            : null;
        return DeviceDeploymentPlan.Create(
            new DeviceId(ProtoUuid.ToGuid(input.DeviceId)),
            input.ExpectedRouterosVersion,
            ToHash(input.ExpectedCapabilityHash),
            ToHash(input.ExpectedConfigurationHash),
            ToHash(input.ExpectedCompatibilityHash),
            ToHash(input.ExpectedGuardContextHash),
            ToHash(input.ExpectedAnchorContextHash),
            ToHash(input.OldArtifactHash),
            oldTargets,
            ToHash(input.NewArtifactHash),
            newTargets,
            activation,
            activation.AsEnumerable().Reverse().ToArray(),
            transitions,
            ttl,
            probes);
    }

    public static PacketPathPairFact ToPacketPath(DeploymentPacketPathPairFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return PacketPathPairFact.Create(
            fact.IngressInterface,
            fact.EgressInterface,
            ToDomain(fact.PathClass),
            fact.HasBridge ? fact.Bridge : null,
            fact.HasVlanId ? fact.VlanId : null);
    }

    public static IReadOnlyDictionary<string, string> ToLiveJumps(IEnumerable<DeploymentLiveJumpFact> facts)
    {
        Dictionary<string, string> jumps = new(StringComparer.Ordinal);
        foreach (DeploymentLiveJumpFact fact in facts)
        {
            jumps[fact.Marker] = fact.JumpTarget;
        }

        return jumps;
    }

    public static IReadOnlyList<(string Name, bool Disabled)> ToWatchdogs(IEnumerable<DeploymentLiveWatchdogFact> facts)
        => facts.Select(static f => (f.Name, f.Disabled)).ToArray();

    public static byte[] ToHashBytes(Sha256 hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        return hash.Value.IsEmpty ? [] : hash.Value.ToByteArray();
    }

    public static Hash256 ToHash(Sha256 hash) => Hash256.Create(ToHashBytes(hash));

    private static Sha256 ToSha256(byte[] bytes)
        => new() { Value = ByteString.CopyFrom(bytes) };

    private static ProtoDiffKind ToProto(AppDiffKind kind)
        => kind switch
        {
            AppDiffKind.ArtifactUnchanged => ProtoDiffKind.ArtifactUnchanged,
            AppDiffKind.ArtifactChanged => ProtoDiffKind.ArtifactChanged,
            _ => ProtoDiffKind.Unspecified,
        };

    private static ProtoProbeKind ToProto(DomainProbeKind kind)
        => kind switch
        {
            DomainProbeKind.RouterPing => ProtoProbeKind.RouterPing,
            DomainProbeKind.ApiSsl => ProtoProbeKind.ApiSsl,
            _ => ProtoProbeKind.Unspecified,
        };

    private static DomainProbeKind ToDomain(ProtoProbeKind kind)
        => kind switch
        {
            ProtoProbeKind.RouterPing => DomainProbeKind.RouterPing,
            ProtoProbeKind.ApiSsl => DomainProbeKind.ApiSsl,
            _ => throw new DomainInvariantException($"Unsupported probe kind '{kind}'."),
        };

    private static DomainPathKind ToDomain(ProtoPathKind kind)
        => kind switch
        {
            ProtoPathKind.CpuFirewall => DomainPathKind.CpuFirewallPath,
            ProtoPathKind.HardwareOffloaded => DomainPathKind.HardwareOffloadedPath,
            ProtoPathKind.Mixed => DomainPathKind.MixedPath,
            ProtoPathKind.Indeterminate => DomainPathKind.Indeterminate,
            _ => throw new DomainInvariantException($"Unsupported packet-path class '{kind}'."),
        };

    private static AnchorTarget ToAnchorTarget(DeploymentAnchorTargetInput input)
    {
        if (!AnchorKey.TryParse(input.Marker, out AnchorKey key))
        {
            throw new DomainInvariantException($"Invalid anchor marker '{input.Marker}'.");
        }

        return new AnchorTarget(key, input.JumpTarget);
    }

    private static DeploymentProbe ToProbe(DeploymentProbeInput input)
        => new(
            ToDomain(input.Kind),
            input.Destination,
            (int)input.TimeoutMilliseconds,
            input.HasSourceAddress ? input.SourceAddress : null,
            input.HasRoutingTable ? input.RoutingTable : null,
            input.HasInterface ? input.Interface : null);
}
