using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mfc.Application.Onboarding;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using DomainAccountFacts = Mfc.Domain.Onboarding.OnboardingServiceAccountFacts;
using DomainAction = Mfc.Domain.Onboarding.OnboardingRecoveryAction;
using DomainFacts = Mfc.Domain.Onboarding.OnboardingDevicePrerequisiteFacts;
using DomainFamily = Mfc.Domain.Inventory.IpAddressFamily;
using DomainIpFacts = Mfc.Domain.Onboarding.OnboardingIpServiceFacts;
using DomainModeFacts = Mfc.Domain.Onboarding.OnboardingDeviceModeFacts;
using DomainNodeKind = Mfc.Domain.Inventory.NodeKind;
using DomainState = Mfc.Domain.Onboarding.OnboardingOperationState;
using DomainSupport = Mfc.Domain.Inventory.SupportState;
using ProtoAction = Mfc.Contracts.Mfc.V1.OnboardingRecoveryAction;
using ProtoFacts = Mfc.Contracts.Mfc.V1.OnboardingDevicePrerequisiteFacts;
using ProtoState = Mfc.Contracts.Mfc.V1.OnboardingOperationState;

namespace Mfc.Controller.Grpc;

/// <summary>Maps onboarding application views onto Contracts wire types (M5-09).</summary>
public static class OnboardingProtoMapper
{
    public static bool IsTerminal(ProtoState state)
        => state is ProtoState.Committed
            or ProtoState.RolledBack
            or ProtoState.Blocked
            or ProtoState.RecoveryRequired;

    public static ProtoState ToProto(DomainState state)
        => state switch
        {
            DomainState.Created => ProtoState.Created,
            DomainState.Prechecking => ProtoState.Prechecking,
            DomainState.StagingBootstrapRoots => ProtoState.StagingBootstrapRoots,
            DomainState.StagingDisabledAnchors => ProtoState.StagingDisabledAnchors,
            DomainState.ArmingWatchdogs => ProtoState.ArmingWatchdogs,
            DomainState.EnablingAnchors => ProtoState.EnablingAnchors,
            DomainState.Verifying => ProtoState.Verifying,
            DomainState.DisarmingWatchdogs => ProtoState.DisarmingWatchdogs,
            DomainState.Committed => ProtoState.Committed,
            DomainState.RollbackPending => ProtoState.RollbackPending,
            DomainState.RollingBack => ProtoState.RollingBack,
            DomainState.RolledBack => ProtoState.RolledBack,
            DomainState.Blocked => ProtoState.Blocked,
            DomainState.RecoveryRequired => ProtoState.RecoveryRequired,
            _ => ProtoState.Unspecified,
        };

    public static ProtoAction ToProto(DomainAction action)
        => action switch
        {
            DomainAction.CleanupRolledBack => ProtoAction.CleanupRolledBack,
            DomainAction.ControllerRollback => ProtoAction.ControllerRollback,
            DomainAction.RecoveryRequired => ProtoAction.RecoveryRequired,
            DomainAction.KeepManaged => ProtoAction.KeepManaged,
            DomainAction.CriticalDrift => ProtoAction.CriticalDrift,
            _ => ProtoAction.Unspecified,
        };

    public static OnboardingPrerequisiteReport ToProto(OnboardingPrerequisiteReportView view)
    {
        OnboardingPrerequisiteReport message = new()
        {
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            Passed = view.Passed,
        };
        foreach (OnboardingFindingView finding in view.Findings)
        {
            OnboardingFinding row = new()
            {
                Code = finding.Code,
                Severity = finding.Severity == OnboardingCodes.SeverityBlocker
                    ? OnboardingFindingSeverity.Blocker
                    : OnboardingFindingSeverity.Warning,
                Message = finding.Message,
            };
            if (finding.DeviceId is Guid deviceId)
            {
                row.DeviceId = ProtoUuid.FromGuid(deviceId);
            }

            if (!string.IsNullOrWhiteSpace(finding.Target))
            {
                row.Target = finding.Target;
            }

            message.Findings.Add(row);
        }

        return message;
    }

    public static OnboardingPlanSummary ToProto(OnboardingPlanSummaryView view)
    {
        OnboardingPlanSummary message = new()
        {
            PlanId = ProtoUuid.FromGuid(view.PlanId),
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            PlanHash = ToSha256(view.PlanHash),
            ExpiresAt = Timestamp.FromDateTimeOffset(view.ExpiresAtUtc),
        };
        foreach (Mfc.Application.Onboarding.OnboardingAnchorPlacementView placement in view.Placements)
        {
            message.Placements.Add(new Mfc.Contracts.Mfc.V1.OnboardingAnchorPlacementView
            {
                Marker = placement.Marker,
                Chain = placement.Chain,
                Family = placement.Family,
                Mode = placement.Mode.Equals("Append", StringComparison.Ordinal)
                    ? OnboardingAnchorPlacementMode.Append
                    : OnboardingAnchorPlacementMode.BeforeStaticRule,
                ExpectedOrdinal = placement.ExpectedOrdinal,
                BeforeLabel = placement.BeforeLabel,
                AfterLabel = placement.AfterLabel,
            });
        }

        return message;
    }

    public static OnboardingOperationSummary ToProto(OnboardingOperationSummaryView view)
    {
        OnboardingOperationSummary message = new()
        {
            OperationId = ProtoUuid.FromGuid(view.OperationId),
            PlanId = ProtoUuid.FromGuid(view.PlanId),
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            State = ToProto(view.State),
            NodeManaged = view.NodeManaged,
        };
        if (!string.IsNullOrWhiteSpace(view.ErrorCode))
        {
            message.ErrorCode = view.ErrorCode;
        }

        message.Timeline.AddRange(view.Timeline);
        return message;
    }

    public static OnboardingRecoveryStatus ToProto(OnboardingRecoveryStatusView view)
    {
        OnboardingRecoveryStatus message = new()
        {
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            OperationState = ToProto(view.OperationState),
            NodeManagementState = view.NodeManagementState,
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

        message.DeviceManagementStates.AddRange(view.DeviceManagementStates);
        return message;
    }

    public static DeviceOnboardingPlan ToDevicePlan(OnboardingDevicePlanInput input, DomainNodeKind kind)
    {
        DeviceId deviceId = new(ProtoUuid.ToGuid(input.DeviceId));
        IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(kind, input.IncludeIpv6);
        List<AnchorPlacement> placements = [];
        uint ordinal = 0;
        foreach (AnchorKey key in keys)
        {
            placements.Add(AnchorPlacement.Create(key.Family, key.Chain, AnchorPlacementMode.Append, ordinal));
            ordinal++;
        }

        TimeSpan? ttl = input.WatchdogTtlSeconds > 0
            ? TimeSpan.FromSeconds(input.WatchdogTtlSeconds)
            : null;
        return DeviceOnboardingPlan.Create(
            deviceId,
            input.ExpectedRouterosVersion,
            ToHash(input.ExpectedCapabilityHash),
            ToHash(input.ExpectedConfigurationHash),
            ToHash(input.ExpectedCompatibilityHash),
            ToHash(input.ExpectedApiServiceHash),
            ToHash(input.ExpectedReadAccountHash),
            ToHash(input.ExpectedDeploymentAccountHash),
            ToHash(input.ExpectedDeviceModeHash),
            ToHash(input.ExpectedGuardHash),
            keys,
            placements,
            watchdogTtl: ttl);
    }

    public static DomainFacts ToFacts(ProtoFacts message)
    {
        DomainSupport support = System.Enum.IsDefined((DomainSupport)message.SupportState)
            ? (DomainSupport)message.SupportState
            : DomainSupport.Unsupported;
        return DomainFacts.Create(
            new DeviceId(ProtoUuid.ToGuid(message.DeviceId)),
            OnboardingPrerequisiteFactFactory.CreateCapability(
                message.VersionMajor,
                message.VersionMinor,
                message.VersionPatch,
                message.VersionChannel,
                support),
            message.ExactSupportedBuild,
            ToIp(message.PlainApi),
            ToIp(message.ApiSsl),
            ToAccount(message.ReadAccount),
            ToAccount(message.DeploymentAccount),
            DomainModeFacts.Create(
                message.DeviceMode?.SchedulerEnabled ?? false,
                message.DeviceMode?.Flagged ?? true),
            expectedApiSslPort: message.ExpectedApiSslPort == 0
                ? ManagementEndpoint.DefaultApiSslPort
                : (ushort)message.ExpectedApiSslPort);
    }

    public static IReadOnlyList<ActualFilterRule> ToLiveAnchors(
        IEnumerable<OnboardingLiveAnchorFact> facts)
    {
        int ordinal = 0;
        List<ActualFilterRule> rules = [];
        foreach (OnboardingLiveAnchorFact fact in facts)
        {
            rules.Add(ActualFilterRule.Create(
                DomainFamily.IPv4,
                string.IsNullOrWhiteSpace(fact.Chain) ? "input" : fact.Chain,
                ordinal++,
                fact.Action,
                fact.Disabled,
                jumpTarget: string.IsNullOrWhiteSpace(fact.JumpTarget) ? null : fact.JumpTarget,
                comment: fact.Marker));
        }

        return rules;
    }

    public static OnboardingSystemNameFacts ToWatchdogNames(IEnumerable<OnboardingLiveWatchdogFact> facts)
    {
        List<string> names = [];
        Dictionary<string, bool> disabled = new(StringComparer.Ordinal);
        foreach (OnboardingLiveWatchdogFact fact in facts)
        {
            names.Add(fact.Name);
            disabled[fact.Name] = fact.Disabled;
        }

        return new OnboardingSystemNameFacts
        {
            ScriptNames = [],
            SchedulerNames = names,
            SchedulerDisabled = disabled,
        };
    }

    public static byte[] ToHashBytes(Sha256 hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        return hash.Value.IsEmpty ? [] : hash.Value.ToByteArray();
    }

    public static Hash256 ToHash(Sha256 hash) => Hash256.Create(ToHashBytes(hash));

    private static Sha256 ToSha256(byte[] bytes)
        => new() { Value = ByteString.CopyFrom(bytes) };

    private static DomainIpFacts ToIp(Mfc.Contracts.Mfc.V1.OnboardingIpServiceFacts? message)
        => DomainIpFacts.Create(
            message?.Found ?? false,
            message?.Disabled ?? false,
            port: message is { HasPort: true } ? (ushort)message.Port : null,
            certificate: message?.Certificate,
            addressPrefixes: message?.AddressPrefixes,
            maxSessions: message is { HasMaxSessions: true } ? message.MaxSessions : null);

    private static DomainAccountFacts ToAccount(Mfc.Contracts.Mfc.V1.OnboardingServiceAccountFacts? message)
        => DomainAccountFacts.Create(
            message?.Name ?? "missing",
            message?.GroupName ?? "missing",
            message?.IsDefaultGroup ?? true,
            message?.Policies ?? [],
            message?.AddressPrefixes.Count > 0 ? message.AddressPrefixes : ["10.0.0.0/24"]);
}
