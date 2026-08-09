using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Snapshots;

namespace Mfc.Application.Mapping;

internal static class ViewMapper
{
    public static SiteView ToView(Site site) => new()
    {
        Id = site.Id.Value,
        Code = site.Code.Value,
        Name = site.Name.Value,
        Status = site.Status,
        RowVersion = site.RowVersion,
    };

    public static NodeView ToView(Node node) => new()
    {
        Id = node.Id.Value,
        SiteId = node.SiteId.Value,
        Name = node.Name.Value,
        DeclaredKind = node.DeclaredKind,
        DeclaredUplinkMode = node.DeclaredUplinkMode,
        Status = node.Status,
        RowVersion = node.RowVersion,
    };

    public static DeviceView ToView(Device device) => new()
    {
        Id = device.Id.Value,
        NodeId = device.NodeId.Value,
        DisplayName = device.DisplayName.Value,
        ManagementHost = device.ManagementEndpoint.Host.Value,
        ManagementPort = device.ManagementEndpoint.Port,
        Role = device.Role,
        Enabled = device.Enabled,
        LastSupportState = device.LastSupportState,
        LastCompletedCaptureId = device.LastCompletedCaptureId,
        RowVersion = device.RowVersion,
    };

    public static SnapshotView ToView(StoredSnapshot snapshot, bool deduplicated = false) => new()
    {
        Id = snapshot.Metadata.Id.Value,
        DeviceId = snapshot.Metadata.DeviceId.Value,
        Status = snapshot.Metadata.Status,
        ConfigurationHashHex = snapshot.Metadata.ConfigurationHash?.ToString(),
        ObservationHashHex = snapshot.Metadata.ObservationHash?.ToString(),
        CapabilityHashHex = snapshot.Metadata.CapabilityHash?.ToString(),
        SnapshotHashHex = snapshot.Metadata.SnapshotHash?.ToString(),
        CompletedAtUtc = snapshot.Metadata.CompletedAtUtc,
        SchemaVersion = snapshot.SchemaVersion,
        OperationId = snapshot.OperationId,
        Deduplicated = deduplicated,
    };
}
