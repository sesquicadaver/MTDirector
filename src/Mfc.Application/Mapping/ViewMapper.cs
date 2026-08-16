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

    public static PolicyRuleView ToView(PolicyRule rule, PolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(document);
        IReadOnlyList<PolicyWarningView> warnings =
            Policies.PolicyRevisionSupport.CollectSoftCatalogWarnings(document, rule.Predicate);
        return ToView(rule, warnings);
    }

    /// <summary>Maps a composed (already resolved) rule; warnings default to empty.</summary>
    public static PolicyRuleView ToView(PolicyRule rule, IReadOnlyList<PolicyWarningView>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return new PolicyRuleView
        {
            Id = rule.Id.Value,
            Family = rule.Family,
            Chain = rule.Chain,
            Stage = rule.Stage,
            Ordinal = rule.Ordinal,
            Enabled = rule.Enabled,
            Predicate = ToView(rule.Predicate),
            Effect = new RuleEffectView
            {
                Kind = rule.Effect.Kind,
                RejectMode = rule.Effect.RejectModeValue,
            },
            Logging = new LogSpecificationView
            {
                Enabled = rule.Logging.Enabled,
                Prefix = rule.Logging.Prefix,
            },
            ExceptionEligible = rule.ExceptionEligible,
            Description = rule.Description,
            Warnings = warnings ?? [],
        };
    }

    public static PolicyRevisionView ToView(PolicyRevision revision, PolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(document);
        PolicyRuleView[] rules = document.Rules.Select(r => ToView(r, document)).ToArray();
        return new PolicyRevisionView
        {
            Id = revision.Id.Value,
            PolicyId = revision.PolicyId.Value,
            RevisionNumber = revision.RevisionNumber,
            SchemaVersion = revision.SchemaVersion,
            State = revision.State,
            ContentHashHex = revision.ContentHash.ToString(),
            ParentContextHashHex = revision.ParentContextHash?.ToString(),
            Kind = document.Kind,
            OwnerScope = document.OwnerScope,
            Rules = rules,
            Warnings = Policies.PolicyRevisionSupport.MergeWarnings(rules),
            ExceptionMetadata = document.ExceptionMetadata is null
                ? null
                : ToView(document.ExceptionMetadata),
            AddressObjects = Policies.PolicyCatalogViewMapper.MapAddresses(document.AddressObjects),
            ServiceObjects = Policies.PolicyCatalogViewMapper.MapServices(document.ServiceObjects),
            ChainContracts = Policies.PolicyCatalogViewMapper.MapChainContracts(document.ChainContracts),
            TestsJson = Policies.PolicyCatalogViewMapper.SerializeTests(document.Tests),
        };
    }

    private static ExceptionMetadataView ToView(ExceptionMetadata metadata) => new()
    {
        TargetScope = metadata.TargetScope,
        TargetScopeId = metadata.TargetScopeId,
        TargetStage = metadata.TargetStage,
        WaivedRuleId = metadata.WaivedRuleId.Value,
        ValidFrom = metadata.ValidFrom,
        ValidUntil = metadata.ValidUntil,
        Reason = metadata.Reason,
        TicketReference = metadata.TicketReference,
        SupersedesExceptionId = metadata.SupersedesExceptionId,
    };

    private static TrafficPredicateView ToView(TrafficPredicate predicate) => new()
    {
        SourceAddresses = ToView(predicate.SourceAddresses),
        DestinationAddresses = ToView(predicate.DestinationAddresses),
        IngressZones = ToView(predicate.IngressZones),
        EgressZones = ToView(predicate.EgressZones),
        Services = ToView(predicate.Services),
        ConnectionStates = predicate.ConnectionStates,
        ConnectionNatStates = predicate.ConnectionNatStates,
        SourceAddressTypes = predicate.SourceAddressTypes,
        DestinationAddressTypes = predicate.DestinationAddressTypes,
        TcpFlags = predicate.TcpFlags is null
            ? null
            : new TcpFlagConstraintView
            {
                RequiredPresent = predicate.TcpFlags.RequiredPresent.ToArray(),
                RequiredAbsent = predicate.TcpFlags.RequiredAbsent.ToArray(),
            },
        IpsecPolicy = predicate.IpsecPolicy is null
            ? null
            : new IpsecPolicyPredicateView
            {
                Direction = predicate.IpsecPolicy.Direction,
                Policy = predicate.IpsecPolicy.Policy,
            },
    };

    private static AddressSelectorView? ToView(AddressSelector? selector)
        => selector is null
            ? null
            : new AddressSelectorView
            {
                Include = selector.Include.Select(static id => id.Value).ToArray(),
                Exclude = selector.Exclude.Select(static id => id.Value).ToArray(),
            };

    private static ZoneSelectorView? ToView(ZoneSelector? selector)
        => selector is null
            ? null
            : new ZoneSelectorView
            {
                Include = selector.Include.Select(static id => id.Value).ToArray(),
                Exclude = selector.Exclude.Select(static id => id.Value).ToArray(),
            };

    private static ServiceSelectorView? ToView(ServiceSelector? selector)
        => selector is null
            ? null
            : new ServiceSelectorView
            {
                Include = selector.Include.Select(static id => id.Value).ToArray(),
            };
}
