using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Models;
using Mfc.Application.Topology;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;
using DomainDeviceRole = Mfc.Domain.Inventory.DeviceRole;
using DomainDeviceSync = Mfc.Domain.Workflow.DeviceSyncClassification;
using DomainNodeKind = Mfc.Domain.Inventory.NodeKind;
using DomainNodeStatus = Mfc.Domain.Inventory.NodeStatus;
using DomainNodeWorkflow = Mfc.Domain.Workflow.NodeWorkflowStatus;
using DomainSiteStatus = Mfc.Domain.Inventory.SiteStatus;
using DomainSupportState = Mfc.Domain.Inventory.SupportState;
using DomainTrust = Mfc.Domain.Inventory.CertificateTrustMode;
using DomainUplink = Mfc.Domain.Inventory.DeclaredUplinkMode;
using ProtoDeviceRole = Mfc.Contracts.Mfc.V1.DeviceRole;
using ProtoDeviceSync = Mfc.Contracts.Mfc.V1.DeviceSyncClassification;
using ProtoDeviceWorkflowProjection = Mfc.Contracts.Mfc.V1.DeviceWorkflowProjection;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoNodeStatus = Mfc.Contracts.Mfc.V1.NodeStatus;
using ProtoNodeWorkflow = Mfc.Contracts.Mfc.V1.NodeWorkflowStatus;
using ProtoSiteStatus = Mfc.Contracts.Mfc.V1.SiteStatus;
using ProtoSupportState = Mfc.Contracts.Mfc.V1.SupportState;
using ProtoTrust = Mfc.Contracts.Mfc.V1.CertificateTrustMode;
using ProtoUplink = Mfc.Contracts.Mfc.V1.DeclaredUplinkMode;

namespace Mfc.Controller.Grpc;

internal static class InventoryProtoMapper
{
    public static Site ToProto(SiteView view) => new()
    {
        Id = ProtoUuid.FromGuid(view.Id),
        Code = view.Code,
        Name = view.Name,
        Status = ToProto(view.Status),
        RowVersion = view.RowVersion,
    };

    public static Node ToProto(NodeView view) => new()
    {
        Id = ProtoUuid.FromGuid(view.Id),
        SiteId = ProtoUuid.FromGuid(view.SiteId),
        Name = view.Name,
        DeclaredKind = ToProto(view.DeclaredKind),
        DeclaredUplinkMode = ToProto(view.DeclaredUplinkMode),
        Status = ToProto(view.Status),
        RowVersion = view.RowVersion,
    };

    public static Device ToProto(DeviceView view)
    {
        Device device = new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            DisplayName = view.DisplayName,
            ManagementHost = view.ManagementHost,
            ManagementPort = view.ManagementPort,
            Enabled = view.Enabled,
            LastSupportState = view.LastSupportState is null
                ? ProtoSupportState.Unspecified
                : ToProto(view.LastSupportState.Value),
            RowVersion = view.RowVersion,
            Role = ToProto(view.Role),
            Reachability = string.IsNullOrWhiteSpace(view.Reachability) ? "Unknown" : view.Reachability,
            SyncClassification = view.SyncClassification is null
                ? ProtoDeviceSync.Unspecified
                : ToProto(view.SyncClassification.Value),
        };
        if (view.LastCompletedCaptureId is Guid captureId)
        {
            device.LastCompletedCaptureId = ProtoUuid.FromGuid(captureId);
        }

        if (!string.IsNullOrWhiteSpace(view.RouterOsVersion))
        {
            device.RouterosVersion = view.RouterOsVersion;
        }

        if (!string.IsNullOrWhiteSpace(view.Model))
        {
            device.Model = view.Model;
        }

        if (view.VrrpRoleLabels.Count > 0)
        {
            device.VrrpRoleLabels.AddRange(view.VrrpRoleLabels);
        }

        if (view.LastSnapshotAtUtc is DateTimeOffset lastSnapshot)
        {
            device.LastSnapshotAt = Timestamp.FromDateTimeOffset(lastSnapshot);
        }

        if (TryHexToSha256(view.DesiredArtifactHashHex, out Sha256? desired))
        {
            device.DesiredArtifactHash = desired;
        }

        if (TryHexToSha256(view.LastCommittedArtifactHashHex, out Sha256? committed))
        {
            device.LastCommittedArtifactHash = committed;
        }

        if (TryHexToSha256(view.ActualManagedResourceHashHex, out Sha256? actual))
        {
            device.ActualManagedResourceHash = actual;
        }

        return device;
    }

    public static NodeDetails ToProto(NodeDetailsView view)
    {
        NodeDetails details = new()
        {
            Node = ToProto(view.Node),
            WorkflowStatus = view.WorkflowStatus is null
                ? ProtoNodeWorkflow.Unspecified
                : ToProto(view.WorkflowStatus.Value),
        };
        details.Devices.AddRange(view.Devices.Select(ToProto));
        foreach (DeviceView device in view.Devices)
        {
            details.DeviceProjections.Add(ToDeviceProjection(device));
        }

        return details;
    }

    public static NodeWorkflow ToProto(Guid nodeId, NodeWorkflowProjectionView view)
    {
        NodeWorkflow message = new()
        {
            NodeId = ProtoUuid.FromGuid(nodeId),
            WorkflowStatus = ToProto(view.NodeStatus),
        };
        foreach (DeviceWorkflowProjectionView device in view.Devices)
        {
            message.Devices.Add(ToDeviceProjection(device));
        }

        return message;
    }

    public static DeviceConnectionSummary ToProto(ConnectionProfileView view)
    {
        DeviceConnectionSummary summary = new()
        {
            DeviceId = ProtoUuid.FromGuid(view.DeviceId),
            Username = view.Username,
            TrustMode = ToProto(view.TrustMode),
            HasPinnedSpki = !string.IsNullOrWhiteSpace(view.PinnedSpkiSha256Hex),
            ConnectTimeoutMs = (uint)view.ConnectTimeoutMs,
            CommandTimeoutMs = (uint)view.CommandTimeoutMs,
            MaxResponseBytes = (ulong)view.MaxResponseBytes,
            RowVersion = view.RowVersion,
        };
        if (!string.IsNullOrWhiteSpace(view.CaProfileRef))
        {
            summary.CaProfileRef = view.CaProfileRef;
        }

        return summary;
    }

    public static ValidateDeviceConnectionResponse ToProto(DeviceDiscoveryView view) => new()
    {
        DeviceId = ProtoUuid.FromGuid(view.DeviceId),
        ObservedIdentity = view.ObservedIdentity,
        SupportState = ToProto(view.SupportState),
        RouterosMutated = view.RouterOsMutated,
    };

    public static ListNeighborCandidatesResponse ToProto(NeighborCandidatesView view)
    {
        ListNeighborCandidatesResponse response = new()
        {
            SeedDeviceId = ProtoUuid.FromGuid(view.SeedDeviceId),
            SeedIdentity = view.SeedIdentity ?? string.Empty,
            RouterosMutated = view.RouterOsMutated,
        };
        foreach (NeighborCandidateView candidate in view.Candidates)
        {
            response.Candidates.Add(new NeighborCandidate
            {
                Address = candidate.Address,
                SuggestedPort = candidate.SuggestedPort,
                Identity = candidate.Identity ?? string.Empty,
                MacAddress = candidate.MacAddress ?? string.Empty,
                Platform = candidate.Platform ?? string.Empty,
                Version = candidate.Version ?? string.Empty,
                Board = candidate.Board ?? string.Empty,
                InterfaceName = candidate.Interface ?? string.Empty,
                Age = candidate.Age ?? string.Empty,
            });
        }

        return response;
    }

    public static VrrpPairConsistencyReport ToProto(VrrpPairConsistencyView view)
    {
        VrrpPairConsistencyReport report = new()
        {
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            Passed = view.Passed,
            MemberCount = (uint)view.MemberCount,
            CaptureCount = (uint)view.CaptureCount,
        };
        foreach (VrrpPairConsistencyFindingView finding in view.Findings)
        {
            VrrpPairConsistencyFinding item = new()
            {
                Code = finding.Code,
                Message = finding.Message,
                Severity = finding.Severity,
            };
            if (!string.IsNullOrWhiteSpace(finding.Subject))
            {
                item.Subject = finding.Subject;
            }

            if (finding.DeviceId is Guid deviceId)
            {
                item.DeviceId = ProtoUuid.FromGuid(deviceId);
            }

            report.Findings.Add(item);
        }

        return report;
    }

    public static DomainSiteStatus ToDomain(ProtoSiteStatus status) => status switch
    {
        ProtoSiteStatus.Draft => DomainSiteStatus.Draft,
        ProtoSiteStatus.Active => DomainSiteStatus.Active,
        ProtoSiteStatus.Disabled => DomainSiteStatus.Disabled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown site status."),
    };

    public static DomainNodeKind ToDomain(ProtoNodeKind kind) => kind switch
    {
        ProtoNodeKind.Router => DomainNodeKind.Router,
        ProtoNodeKind.Vrrp => DomainNodeKind.Vrrp,
        ProtoNodeKind.Switch => DomainNodeKind.Switch,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown node kind."),
    };

    public static DomainUplink ToDomain(ProtoUplink mode) => mode switch
    {
        ProtoUplink.None => DomainUplink.None,
        ProtoUplink.One => DomainUplink.One,
        ProtoUplink.Failover => DomainUplink.Failover,
        ProtoUplink.Balanced => DomainUplink.Balanced,
        ProtoUplink.Mixed => DomainUplink.Mixed,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown uplink mode."),
    };

    public static DomainDeviceRole ToDomain(ProtoDeviceRole role) => role switch
    {
        ProtoDeviceRole.Router => DomainDeviceRole.Router,
        ProtoDeviceRole.L3Switch => DomainDeviceRole.L3Switch,
        ProtoDeviceRole.L2Switch => DomainDeviceRole.L2Switch,
        ProtoDeviceRole.Unknown => DomainDeviceRole.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown device role."),
    };

    public static DomainTrust ToDomain(ProtoTrust trust) => trust switch
    {
        ProtoTrust.InternalCa => DomainTrust.InternalCa,
        ProtoTrust.SpkiPin => DomainTrust.SpkiPin,
        _ => throw new ArgumentOutOfRangeException(nameof(trust), trust, "Unknown trust mode."),
    };

    public static Hash256? ToHash(Sha256? sha)
    {
        if (sha is null || sha.Value.Length == 0)
        {
            return null;
        }

        if (sha.Value.Length != 32)
        {
            throw new ArgumentException("Sha256.value must be exactly 32 bytes.");
        }

        return Hash256.Create(sha.Value.ToByteArray());
    }

    private static ProtoDeviceWorkflowProjection ToDeviceProjection(DeviceView view)
    {
        ProtoDeviceWorkflowProjection projection = new()
        {
            DeviceId = ProtoUuid.FromGuid(view.Id),
            SyncClassification = view.SyncClassification is null
                ? ProtoDeviceSync.Unspecified
                : ToProto(view.SyncClassification.Value),
            ContributingStatus = view.SyncClassification is null
                ? ProtoNodeWorkflow.Unspecified
                : ToContributingProto(view.SyncClassification.Value),
        };
        if (TryHexToSha256(view.DesiredArtifactHashHex, out Sha256? desired))
        {
            projection.DesiredArtifactHash = desired;
        }

        if (TryHexToSha256(view.LastCommittedArtifactHashHex, out Sha256? committed))
        {
            projection.LastCommittedArtifactHash = committed;
        }

        if (TryHexToSha256(view.ActualManagedResourceHashHex, out Sha256? actual))
        {
            projection.ActualManagedResourceHash = actual;
        }

        return projection;
    }

    private static ProtoDeviceWorkflowProjection ToDeviceProjection(DeviceWorkflowProjectionView view)
    {
        ProtoDeviceWorkflowProjection projection = new()
        {
            DeviceId = ProtoUuid.FromGuid(view.DeviceId),
            SyncClassification = ToProto(view.SyncClassification),
            ContributingStatus = view.ContributingStatus is null
                ? ProtoNodeWorkflow.Unspecified
                : ToProto(view.ContributingStatus.Value),
        };
        if (TryHexToSha256(view.HashState.DesiredArtifactHashHex, out Sha256? desired))
        {
            projection.DesiredArtifactHash = desired;
        }

        if (TryHexToSha256(view.HashState.LastCommittedArtifactHashHex, out Sha256? committed))
        {
            projection.LastCommittedArtifactHash = committed;
        }

        if (TryHexToSha256(view.HashState.ActualManagedResourceHashHex, out Sha256? actual))
        {
            projection.ActualManagedResourceHash = actual;
        }

        return projection;
    }

    private static bool TryHexToSha256(string? hex, out Sha256? sha)
    {
        sha = null;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        try
        {
            byte[] bytes = Hash256.ParseHex(hex).Bytes.ToArray();
            sha = new Sha256 { Value = ByteString.CopyFrom(bytes) };
            return true;
        }
        catch (DomainInvariantException)
        {
            return false;
        }
    }

    private static ProtoSiteStatus ToProto(DomainSiteStatus status) => status switch
    {
        DomainSiteStatus.Draft => ProtoSiteStatus.Draft,
        DomainSiteStatus.Active => ProtoSiteStatus.Active,
        DomainSiteStatus.Disabled => ProtoSiteStatus.Disabled,
        _ => ProtoSiteStatus.Unspecified,
    };

    private static ProtoNodeKind ToProto(DomainNodeKind kind) => kind switch
    {
        DomainNodeKind.Router => ProtoNodeKind.Router,
        DomainNodeKind.Vrrp => ProtoNodeKind.Vrrp,
        DomainNodeKind.Switch => ProtoNodeKind.Switch,
        _ => ProtoNodeKind.Unspecified,
    };

    private static ProtoUplink ToProto(DomainUplink mode) => mode switch
    {
        DomainUplink.None => ProtoUplink.None,
        DomainUplink.One => ProtoUplink.One,
        DomainUplink.Failover => ProtoUplink.Failover,
        DomainUplink.Balanced => ProtoUplink.Balanced,
        DomainUplink.Mixed => ProtoUplink.Mixed,
        _ => ProtoUplink.Unspecified,
    };

    private static ProtoNodeStatus ToProto(DomainNodeStatus status) => status switch
    {
        DomainNodeStatus.Draft => ProtoNodeStatus.Draft,
        DomainNodeStatus.Active => ProtoNodeStatus.Active,
        DomainNodeStatus.Disabled => ProtoNodeStatus.Disabled,
        _ => ProtoNodeStatus.Unspecified,
    };

    private static ProtoDeviceRole ToProto(DomainDeviceRole role) => role switch
    {
        DomainDeviceRole.Router => ProtoDeviceRole.Router,
        DomainDeviceRole.L3Switch => ProtoDeviceRole.L3Switch,
        DomainDeviceRole.L2Switch => ProtoDeviceRole.L2Switch,
        DomainDeviceRole.Unknown => ProtoDeviceRole.Unknown,
        _ => ProtoDeviceRole.Unspecified,
    };

    private static ProtoSupportState ToProto(DomainSupportState state) => state switch
    {
        DomainSupportState.Supported => ProtoSupportState.Supported,
        DomainSupportState.ReadOnly => ProtoSupportState.ReadOnly,
        DomainSupportState.NeedsRevalidation => ProtoSupportState.NeedsRevalidation,
        DomainSupportState.Unsupported => ProtoSupportState.Unsupported,
        _ => ProtoSupportState.Unspecified,
    };

    private static ProtoTrust ToProto(DomainTrust trust) => trust switch
    {
        DomainTrust.InternalCa => ProtoTrust.InternalCa,
        DomainTrust.SpkiPin => ProtoTrust.SpkiPin,
        _ => ProtoTrust.Unspecified,
    };

    private static ProtoNodeWorkflow ToProto(DomainNodeWorkflow status) => status switch
    {
        DomainNodeWorkflow.InventoryIncomplete => ProtoNodeWorkflow.InventoryIncomplete,
        DomainNodeWorkflow.ConnectionInvalid => ProtoNodeWorkflow.ConnectionInvalid,
        DomainNodeWorkflow.CaptureRequired => ProtoNodeWorkflow.CaptureRequired,
        DomainNodeWorkflow.TopologyBlocked => ProtoNodeWorkflow.TopologyBlocked,
        DomainNodeWorkflow.OnboardingRequired => ProtoNodeWorkflow.OnboardingRequired,
        DomainNodeWorkflow.OnboardingInProgress => ProtoNodeWorkflow.OnboardingInProgress,
        DomainNodeWorkflow.PolicyRequired => ProtoNodeWorkflow.PolicyRequired,
        DomainNodeWorkflow.AnalysisRequired => ProtoNodeWorkflow.AnalysisRequired,
        DomainNodeWorkflow.AnalysisBlocked => ProtoNodeWorkflow.AnalysisBlocked,
        DomainNodeWorkflow.PendingDeployment => ProtoNodeWorkflow.PendingDeployment,
        DomainNodeWorkflow.DeploymentInProgress => ProtoNodeWorkflow.DeploymentInProgress,
        DomainNodeWorkflow.Synchronized => ProtoNodeWorkflow.Synchronized,
        DomainNodeWorkflow.Drifted => ProtoNodeWorkflow.Drifted,
        DomainNodeWorkflow.RecoveryRequired => ProtoNodeWorkflow.RecoveryRequired,
        _ => ProtoNodeWorkflow.Unspecified,
    };

    private static ProtoDeviceSync ToProto(DomainDeviceSync classification) => classification switch
    {
        DomainDeviceSync.Synchronized => ProtoDeviceSync.Synchronized,
        DomainDeviceSync.PendingDeployment => ProtoDeviceSync.PendingDeployment,
        DomainDeviceSync.Drifted => ProtoDeviceSync.Drifted,
        DomainDeviceSync.RecoveryRequired => ProtoDeviceSync.RecoveryRequired,
        DomainDeviceSync.Incomplete => ProtoDeviceSync.Incomplete,
        _ => ProtoDeviceSync.Unspecified,
    };

    private static ProtoNodeWorkflow ToContributingProto(DomainDeviceSync classification)
        => classification switch
        {
            DomainDeviceSync.RecoveryRequired => ProtoNodeWorkflow.RecoveryRequired,
            DomainDeviceSync.Drifted => ProtoNodeWorkflow.Drifted,
            DomainDeviceSync.PendingDeployment => ProtoNodeWorkflow.PendingDeployment,
            DomainDeviceSync.Synchronized => ProtoNodeWorkflow.Synchronized,
            _ => ProtoNodeWorkflow.Unspecified,
        };
}
