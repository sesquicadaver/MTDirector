using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
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

    public static DeviceView ToView(Device device, DateTimeOffset? lastSnapshotAtUtc = null) => new()
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
        // Observation fields stay unset until discovery/topology probes populate them.
        RouterOsVersion = null,
        Model = null,
        Reachability = "Unknown",
        VrrpRoleLabels = [],
        LastSnapshotAtUtc = lastSnapshotAtUtc,
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

    public static ZoneDefinitionView ToView(ZoneDefinition zone) => new()
    {
        Id = zone.Id.Value,
        OwnerScope = zone.OwnerScope,
        OwnerId = zone.OwnerId,
        Key = zone.Key.Value,
        Name = zone.Name.Value,
        Description = zone.Description,
        RowVersion = zone.RowVersion,
    };

    public static NodeZoneBindingView ToView(NodeZoneBinding binding) => new()
    {
        Id = binding.Id.Value,
        NodeId = binding.NodeId.Value,
        ZoneId = binding.ZoneId.Value,
        Kind = binding.Kind,
        Values = binding.Values.ToArray(),
        ExpectedDependencyHashHex = binding.ExpectedDependencyHash.ToString(),
        LastResolvedDependencyHashHex = binding.LastResolvedDependencyHash?.ToString(),
        AnalysisStale = binding.AnalysisStale,
        RowVersion = binding.RowVersion,
    };

    public static ZoneBindingResolveView ToView(
        ZoneBindingResolveResult result,
        NodeZoneBinding binding)
    {
        // Wire Binding.AnalysisStale matches this device/result row; SoT may OR-aggregate across devices.
        NodeZoneBindingView bindingView = ToView(binding);
        bindingView = new NodeZoneBindingView
        {
            Id = bindingView.Id,
            NodeId = bindingView.NodeId,
            ZoneId = bindingView.ZoneId,
            Kind = bindingView.Kind,
            Values = bindingView.Values,
            ExpectedDependencyHashHex = bindingView.ExpectedDependencyHashHex,
            LastResolvedDependencyHashHex = bindingView.LastResolvedDependencyHashHex,
            AnalysisStale = result.AnalysisStale,
            RowVersion = bindingView.RowVersion,
        };
        return new ZoneBindingResolveView
        {
            BindingId = result.BindingId.Value,
            ZoneId = result.ZoneId.Value,
            DeviceId = result.DeviceId.Value,
            ResolvedMembers = result.ResolvedMembers.ToArray(),
            FreshDependencyHashHex = result.FreshDependencyHash.ToString(),
            AnalysisStale = result.AnalysisStale,
            Blockers = result.Blockers.Select(b => new ZoneResolveBlockerView
            {
                Code = b.Code,
                Message = b.Message,
                Subject = b.Subject,
            }).ToArray(),
            Binding = bindingView,
        };
    }
}
