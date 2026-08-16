using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Kind of a generated VRRP protected flow (Policy Model §47.1–§47.2). Not a RouterOS write.</summary>
public enum VrrpProtectedFlowKind : byte
{
    Advertisement = 0,
    Sync = 1,
}

/// <summary>Uplink identity used for zone-coverage checks (Policy Model §48.1).</summary>
public sealed class UplinkCoverageFact
{
    public required string Key { get; init; }

    public required UplinkTrafficMode Mode { get; init; }

    public string? ZoneKey { get; init; }

    public static UplinkCoverageFact Create(string key, UplinkTrafficMode mode, string? zoneKey)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainInvariantException("Uplink coverage key is required.");
        }

        return new UplinkCoverageFact
        {
            Key = key.Trim(),
            Mode = mode,
            ZoneKey = string.IsNullOrWhiteSpace(zoneKey) ? null : zoneKey.Trim(),
        };
    }
}

/// <summary>VRRP instance configuration (role is not stored here).</summary>
public sealed class VrrpInstanceFacts
{
    public required IpAddressFamily Family { get; init; }

    public required byte Vrid { get; init; }

    public required string ParentInterface { get; init; }

    public required bool Disabled { get; init; }

    public required bool SyncConnectionTracking { get; init; }

    public required ushort SyncPort { get; init; }

    public string? RemoteAddress { get; init; }

    public static VrrpInstanceFacts Create(
        IpAddressFamily family,
        byte vrid,
        string parentInterface,
        bool disabled = false,
        bool syncConnectionTracking = false,
        ushort syncPort = TopologyDependencyAnalysis.DefaultVrrpSyncPort,
        string? remoteAddress = null)
    {
        if (vrid == 0)
        {
            throw new DomainInvariantException("VRRP VRID must be 1–255.");
        }

        if (string.IsNullOrWhiteSpace(parentInterface))
        {
            throw new DomainInvariantException("VRRP parent interface is required.");
        }

        if (syncPort == 0)
        {
            throw new DomainInvariantException("VRRP sync port must be non-zero.");
        }

        return new VrrpInstanceFacts
        {
            Family = family,
            Vrid = vrid,
            ParentInterface = parentInterface.Trim(),
            Disabled = disabled,
            SyncConnectionTracking = syncConnectionTracking,
            SyncPort = syncPort,
            RemoteAddress = string.IsNullOrWhiteSpace(remoteAddress) ? null : remoteAddress.Trim(),
        };
    }
}

/// <summary>Observed per-VRID role. Must not be collapsed to a global master flag (AC#5, AC#14).</summary>
public sealed class VrrpRoleAssignment
{
    public required string DeviceId { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required byte Vrid { get; init; }

    public required string ParentInterface { get; init; }

    public required VrrpMemberObservedState Role { get; init; }

    public static VrrpRoleAssignment Create(
        string deviceId,
        IpAddressFamily family,
        byte vrid,
        string parentInterface,
        VrrpMemberObservedState role)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new DomainInvariantException("VRRP role assignment requires a device id.");
        }

        if (vrid == 0)
        {
            throw new DomainInvariantException("VRRP VRID must be 1–255.");
        }

        if (string.IsNullOrWhiteSpace(parentInterface))
        {
            throw new DomainInvariantException("VRRP parent interface is required.");
        }

        return new VrrpRoleAssignment
        {
            DeviceId = deviceId.Trim(),
            Family = family,
            Vrid = vrid,
            ParentInterface = parentInterface.Trim(),
            Role = role,
        };
    }
}

/// <summary>Routing table configuration identity (name only; not active route state).</summary>
public sealed class RoutingTableFact
{
    public required string Name { get; init; }

    public required bool Disabled { get; init; }

    public static RoutingTableFact Create(string name, bool disabled = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainInvariantException("Routing table name is required.");
        }

        return new RoutingTableFact { Name = name.Trim(), Disabled = disabled };
    }
}

/// <summary>Routing rule configuration identity (Policy Model §48).</summary>
public sealed class RoutingRuleFact
{
    public required int Ordinal { get; init; }

    public string? Action { get; init; }

    public string? Table { get; init; }

    public string? RoutingMark { get; init; }

    public required bool Disabled { get; init; }

    public static RoutingRuleFact Create(
        int ordinal,
        string? action = null,
        string? table = null,
        string? routingMark = null,
        bool disabled = false)
        => new()
        {
            Ordinal = ordinal,
            Action = string.IsNullOrWhiteSpace(action) ? null : action.Trim(),
            Table = string.IsNullOrWhiteSpace(table) ? null : table.Trim(),
            RoutingMark = string.IsNullOrWhiteSpace(routingMark) ? null : routingMark.Trim(),
            Disabled = disabled,
        };
}

/// <summary>NAT / RAW / Mangle rule facts independent of RouterOs discovery types.</summary>
public sealed class FacilityRuleFact
{
    public required IpAddressFamily Family { get; init; }

    public required int Ordinal { get; init; }

    public string? Chain { get; init; }

    public string? Action { get; init; }

    public required bool Disabled { get; init; }

    public string? RoutingMark { get; init; }

    public string? NewRoutingMark { get; init; }

    public string? PerConnectionClassifier { get; init; }

    public string? ConnectionMark { get; init; }

    public string? PacketMark { get; init; }

    public string? NewConnectionMark { get; init; }

    public string? NewPacketMark { get; init; }

    public string? ToAddresses { get; init; }

    public string? ToPorts { get; init; }

    public string? ConnectionState { get; init; }

    public string? ConnectionNatState { get; init; }

    public IReadOnlyList<string> UnsupportedMatchers { get; init; } = [];

    public static FacilityRuleFact Create(
        IpAddressFamily family,
        int ordinal,
        string? chain = null,
        string? action = null,
        bool disabled = false,
        string? routingMark = null,
        string? newRoutingMark = null,
        string? perConnectionClassifier = null,
        string? connectionMark = null,
        string? packetMark = null,
        string? newConnectionMark = null,
        string? newPacketMark = null,
        string? toAddresses = null,
        string? toPorts = null,
        string? connectionState = null,
        string? connectionNatState = null,
        IReadOnlyList<string>? unsupportedMatchers = null)
        => new()
        {
            Family = family,
            Ordinal = ordinal,
            Chain = string.IsNullOrWhiteSpace(chain) ? null : chain.Trim(),
            Action = string.IsNullOrWhiteSpace(action) ? null : action.Trim(),
            Disabled = disabled,
            RoutingMark = string.IsNullOrWhiteSpace(routingMark) ? null : routingMark.Trim(),
            NewRoutingMark = string.IsNullOrWhiteSpace(newRoutingMark) ? null : newRoutingMark.Trim(),
            PerConnectionClassifier = string.IsNullOrWhiteSpace(perConnectionClassifier)
                ? null
                : perConnectionClassifier.Trim(),
            ConnectionMark = string.IsNullOrWhiteSpace(connectionMark) ? null : connectionMark.Trim(),
            PacketMark = string.IsNullOrWhiteSpace(packetMark) ? null : packetMark.Trim(),
            NewConnectionMark = string.IsNullOrWhiteSpace(newConnectionMark) ? null : newConnectionMark.Trim(),
            NewPacketMark = string.IsNullOrWhiteSpace(newPacketMark) ? null : newPacketMark.Trim(),
            ToAddresses = string.IsNullOrWhiteSpace(toAddresses) ? null : toAddresses.Trim(),
            ToPorts = string.IsNullOrWhiteSpace(toPorts) ? null : toPorts.Trim(),
            ConnectionState = string.IsNullOrWhiteSpace(connectionState) ? null : connectionState.Trim(),
            ConnectionNatState = string.IsNullOrWhiteSpace(connectionNatState) ? null : connectionNatState.Trim(),
            UnsupportedMatchers = unsupportedMatchers ?? [],
        };
}

/// <summary>Default-route runtime observation — never enters topology-dependency context hash (AC#14).</summary>
public sealed class DefaultRouteObservation
{
    public required IpAddressFamily Family { get; init; }

    public string? Table { get; init; }

    public string? Gateway { get; init; }

    public string? Active { get; init; }

    public string? GatewayStatus { get; init; }

    public static DefaultRouteObservation Create(
        IpAddressFamily family,
        string? table = null,
        string? gateway = null,
        string? active = null,
        string? gatewayStatus = null)
        => new()
        {
            Family = family,
            Table = string.IsNullOrWhiteSpace(table) ? null : table.Trim(),
            Gateway = string.IsNullOrWhiteSpace(gateway) ? null : gateway.Trim(),
            Active = string.IsNullOrWhiteSpace(active) ? null : active.Trim(),
            GatewayStatus = string.IsNullOrWhiteSpace(gatewayStatus) ? null : gatewayStatus.Trim(),
        };
}

/// <summary>Candidate filter surface used for DSTNAT / RAW / SWITCH / INVALID-drop checks.</summary>
public sealed class CandidatePolicySurface
{
    public static CandidatePolicySurface None { get; } = new()
    {
        HasForward = false,
        HasDstNatMatcher = false,
        HasStatefulConnectionMatcher = false,
        HandlesUntracked = false,
        DropsInvalid = false,
    };

    public required bool HasForward { get; init; }

    public required bool HasDstNatMatcher { get; init; }

    public required bool HasStatefulConnectionMatcher { get; init; }

    public required bool HandlesUntracked { get; init; }

    public required bool DropsInvalid { get; init; }

    public static CandidatePolicySurface Create(
        bool hasForward = false,
        bool hasDstNatMatcher = false,
        bool hasStatefulConnectionMatcher = false,
        bool handlesUntracked = false,
        bool dropsInvalid = false)
        => new()
        {
            HasForward = hasForward,
            HasDstNatMatcher = hasDstNatMatcher,
            HasStatefulConnectionMatcher = hasStatefulConnectionMatcher,
            HandlesUntracked = handlesUntracked,
            DropsInvalid = dropsInvalid,
        };
}

/// <summary>Caller-supplied inventory + candidate flags for topology-dependency analysis (M2-14).</summary>
public sealed class TopologyDependencyProfile
{
    public required NodeKind Kind { get; init; }

    public required DeclaredUplinkMode UplinkMode { get; init; }

    public IReadOnlyList<UplinkCoverageFact> Uplinks { get; init; } = [];

    public IReadOnlyList<string> DeclaredVrrpMemberIds { get; init; } = [];

    public IReadOnlyList<string> ObservedVrrpMemberIds { get; init; } = [];

    public string ObservingDeviceId { get; init; } = "local";

    public CandidatePolicySurface Candidate { get; init; } = CandidatePolicySurface.None;

    /// <summary>Fail-closed: SWITCH chip is unknown until proven (Policy Model §53).</summary>
    public bool SwitchHardwareProfileKnown { get; init; }

    /// <summary>Fail-closed: SWITCH transit is unproven until IP-firewall path is evidenced.</summary>
    public bool SwitchTransitPathProven { get; init; }

    public static TopologyDependencyProfile Create(
        NodeKind kind = NodeKind.Router,
        DeclaredUplinkMode uplinkMode = DeclaredUplinkMode.None,
        IReadOnlyList<UplinkCoverageFact>? uplinks = null,
        IReadOnlyList<string>? declaredVrrpMemberIds = null,
        IReadOnlyList<string>? observedVrrpMemberIds = null,
        string? observingDeviceId = null,
        CandidatePolicySurface? candidate = null,
        bool switchHardwareProfileKnown = false,
        bool switchTransitPathProven = false)
        => new()
        {
            Kind = kind,
            UplinkMode = uplinkMode,
            Uplinks = uplinks ?? [],
            DeclaredVrrpMemberIds = declaredVrrpMemberIds ?? [],
            ObservedVrrpMemberIds = observedVrrpMemberIds ?? [],
            ObservingDeviceId = string.IsNullOrWhiteSpace(observingDeviceId)
                ? "local"
                : observingDeviceId.Trim(),
            Candidate = candidate ?? CandidatePolicySurface.None,
            SwitchHardwareProfileKnown = switchHardwareProfileKnown,
            SwitchTransitPathProven = switchTransitPathProven,
        };
}

/// <summary>Complete analysis input. Operational role and active routes are observation-only.</summary>
public sealed class TopologyDependencyFacts
{
    public required NodeKind Kind { get; init; }

    public required DeclaredUplinkMode UplinkMode { get; init; }

    public required IReadOnlyList<UplinkCoverageFact> Uplinks { get; init; }

    public required IReadOnlyList<VrrpInstanceFacts> VrrpInstances { get; init; }

    public required IReadOnlyList<string> DeclaredVrrpMemberIds { get; init; }

    public required IReadOnlyList<string> ObservedVrrpMemberIds { get; init; }

    public required IReadOnlyList<VrrpRoleAssignment> RoleVector { get; init; }

    public required IReadOnlyList<RoutingTableFact> RoutingTables { get; init; }

    public required IReadOnlyList<RoutingRuleFact> RoutingRules { get; init; }

    public string? RpFilter { get; init; }

    public required IReadOnlyList<FacilityRuleFact> RawRules { get; init; }

    public required IReadOnlyList<FacilityRuleFact> NatRules { get; init; }

    public required IReadOnlyList<FacilityRuleFact> MangleRules { get; init; }

    public required CandidatePolicySurface Candidate { get; init; }

    public required bool SwitchHardwareProfileKnown { get; init; }

    public required bool SwitchTransitPathProven { get; init; }

    public IReadOnlyList<DefaultRouteObservation> DefaultRouteObservations { get; init; } = [];

    public static TopologyDependencyFacts Create(
        NodeKind kind = NodeKind.Router,
        DeclaredUplinkMode uplinkMode = DeclaredUplinkMode.None,
        IReadOnlyList<UplinkCoverageFact>? uplinks = null,
        IReadOnlyList<VrrpInstanceFacts>? vrrpInstances = null,
        IReadOnlyList<string>? declaredVrrpMemberIds = null,
        IReadOnlyList<string>? observedVrrpMemberIds = null,
        IReadOnlyList<VrrpRoleAssignment>? roleVector = null,
        IReadOnlyList<RoutingTableFact>? routingTables = null,
        IReadOnlyList<RoutingRuleFact>? routingRules = null,
        string? rpFilter = null,
        IReadOnlyList<FacilityRuleFact>? rawRules = null,
        IReadOnlyList<FacilityRuleFact>? natRules = null,
        IReadOnlyList<FacilityRuleFact>? mangleRules = null,
        CandidatePolicySurface? candidate = null,
        bool switchHardwareProfileKnown = false,
        bool switchTransitPathProven = false,
        IReadOnlyList<DefaultRouteObservation>? defaultRouteObservations = null)
        => new()
        {
            Kind = kind,
            UplinkMode = uplinkMode,
            Uplinks = uplinks ?? [],
            VrrpInstances = vrrpInstances ?? [],
            DeclaredVrrpMemberIds = declaredVrrpMemberIds ?? [],
            ObservedVrrpMemberIds = observedVrrpMemberIds ?? [],
            RoleVector = roleVector ?? [],
            RoutingTables = routingTables ?? [],
            RoutingRules = routingRules ?? [],
            RpFilter = string.IsNullOrWhiteSpace(rpFilter) ? null : rpFilter.Trim(),
            RawRules = rawRules ?? [],
            NatRules = natRules ?? [],
            MangleRules = mangleRules ?? [],
            Candidate = candidate ?? CandidatePolicySurface.None,
            SwitchHardwareProfileKnown = switchHardwareProfileKnown,
            SwitchTransitPathProven = switchTransitPathProven,
            DefaultRouteObservations = defaultRouteObservations ?? [],
        };
}

/// <summary>Generated protected VRRP flow. Analyzer never writes RouterOS filter/NAT/RAW/Mangle.</summary>
public sealed class ProtectedVrrpFlow
{
    public required IpAddressFamily Family { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required byte Protocol { get; init; }

    public required string Destination { get; init; }

    public byte? HopLimitOrTtl { get; init; }

    public required string Interface { get; init; }

    public required VrrpProtectedFlowKind Kind { get; init; }

    public ushort? DestinationPort { get; init; }

    public string? RemoteAddress { get; init; }
}

/// <summary>One topology-dependency finding. Subject is uplink key, VRID, or facility ordinal — not a UUID.</summary>
public sealed class TopologyDependencyFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Outcome of <see cref="TopologyDependencyAnalysis.Analyze"/>.</summary>
public sealed class TopologyDependencyAnalysisResult
{
    public required IReadOnlyList<TopologyDependencyFinding> Findings { get; init; }

    public required IReadOnlyList<ProtectedVrrpFlow> ProtectedFlows { get; init; }

    /// <summary>Per family+VRID+interface+device observed roles. Never a collapsed global master.</summary>
    public required IReadOnlyList<VrrpRoleAssignment> RoleVector { get; init; }

    /// <summary>SHA-256 of configuration identity (excludes VRRP role and active default routes).</summary>
    public required Hash256 TopologyDependencyContextHash { get; init; }

    /// <summary>SHA-256 of operational observations (role + active routes). Not a policy-hash slot.</summary>
    public required Hash256 TopologyObservationHash { get; init; }

    /// <summary>Always false: analyzer never collapses split-master into a global role.</summary>
    public required bool HasCollapsedGlobalMaster { get; init; }

    public bool HasBlockers
        => Findings.Any(static f => f.Severity == TopologyDependencyAnalysisCodes.SeverityBlocker);
}
