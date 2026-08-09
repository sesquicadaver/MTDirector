using Google.Protobuf.WellKnownTypes;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain.Inventory.Primitives;
using DomainTrust = Mfc.Domain.Inventory.CertificateTrustMode;
using DomainDeviceRole = Mfc.Domain.Inventory.DeviceRole;
using DomainNodeKind = Mfc.Domain.Inventory.NodeKind;
using DomainNodeStatus = Mfc.Domain.Inventory.NodeStatus;
using DomainSiteStatus = Mfc.Domain.Inventory.SiteStatus;
using DomainSupportState = Mfc.Domain.Inventory.SupportState;
using DomainUplink = Mfc.Domain.Inventory.DeclaredUplinkMode;
using ProtoDeviceRole = Mfc.Contracts.Mfc.V1.DeviceRole;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoNodeStatus = Mfc.Contracts.Mfc.V1.NodeStatus;
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

        return device;
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
}
