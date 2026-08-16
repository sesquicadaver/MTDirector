using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Topology and dependency safety validation (Policy Model §47–§53 / M2-14).
/// Generates protected VRRP flows; does not write RouterOS NAT/RAW/Mangle/VRRP or disable primary WAN.
/// Operational VRRP role and active default routes do not enter the context hash (AC#14).
/// </summary>
public static class TopologyDependencyAnalysis
{
    public const string AnalyzerVersion = "mfc.topology-dependency.v1";

    public const string TopologyDependencyContextPrefix = "mfc.policy.topology_dependency_context.v1";

    public const string TopologyObservationPrefix = "mfc.policy.topology_dependency_observation.v1";

    public const string AnalysisContextPrefix = "mfc.policy.analysis_context.v1";

    public const byte VrrpProtocol = 112;

    public const ushort DefaultVrrpSyncPort = 8275;

    public const string Ipv4VrrpMulticast = "224.0.0.18";

    public const string Ipv6VrrpMulticast = "ff02::12";

    public const string SyncMembersPlaceholder = "vrrp-members";

    /// <summary>Validates VRRP, multi-WAN, RAW, NAT, Mangle, and SWITCH constraints on one analysis snapshot.</summary>
    public static TopologyDependencyAnalysisResult Analyze(TopologyDependencyFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(facts.Uplinks);
        ArgumentNullException.ThrowIfNull(facts.VrrpInstances);
        ArgumentNullException.ThrowIfNull(facts.DeclaredVrrpMemberIds);
        ArgumentNullException.ThrowIfNull(facts.ObservedVrrpMemberIds);
        ArgumentNullException.ThrowIfNull(facts.RoleVector);
        ArgumentNullException.ThrowIfNull(facts.RoutingTables);
        ArgumentNullException.ThrowIfNull(facts.RoutingRules);
        ArgumentNullException.ThrowIfNull(facts.RawRules);
        ArgumentNullException.ThrowIfNull(facts.NatRules);
        ArgumentNullException.ThrowIfNull(facts.MangleRules);
        ArgumentNullException.ThrowIfNull(facts.Candidate);

        List<TopologyDependencyFinding> findings = [];
        List<ProtectedVrrpFlow> flows = [];
        CheckVrrp(facts, findings, flows);
        CheckUplinkCoverage(facts, findings);
        CheckStrictRpf(facts, findings);
        CheckInvalidDrop(facts, findings);
        CheckRaw(facts, findings);
        CheckNat(facts, findings);
        CheckMangle(facts, findings);
        CheckSwitch(facts, findings);

        IReadOnlyList<TopologyDependencyFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.Subject, f.Message))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Subject ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();

        IReadOnlyList<ProtectedVrrpFlow> orderedFlows = flows
            .OrderBy(static f => f.Family)
            .ThenBy(static f => f.Kind)
            .ThenBy(static f => f.Chain)
            .ThenBy(static f => f.Interface, StringComparer.Ordinal)
            .ThenBy(static f => f.Destination, StringComparer.Ordinal)
            .ToArray();

        IReadOnlyList<VrrpRoleAssignment> roleVector = facts.RoleVector
            .OrderBy(static r => r.Family)
            .ThenBy(static r => r.Vrid)
            .ThenBy(static r => r.ParentInterface, StringComparer.Ordinal)
            .ThenBy(static r => r.DeviceId, StringComparer.Ordinal)
            .ToArray();

        return new TopologyDependencyAnalysisResult
        {
            Findings = ordered,
            ProtectedFlows = orderedFlows,
            RoleVector = roleVector,
            TopologyDependencyContextHash = HashTopologyDependencyContext(facts),
            TopologyObservationHash = HashTopologyObservation(facts),
            HasCollapsedGlobalMaster = false,
        };
    }

    /// <summary>SHA-256 of configuration identity. Excludes VRRP role and active default-route observations.</summary>
    public static Hash256 HashTopologyDependencyContext(TopologyDependencyFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, TopologyDependencyContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData([(byte)(int)facts.Kind]);
        hasher.AppendData([(byte)(int)facts.UplinkMode]);
        AppendUtf8(hasher, facts.RpFilter ?? string.Empty);
        hasher.AppendData([(byte)0]);
        hasher.AppendData([(byte)(facts.SwitchHardwareProfileKnown ? 1 : 0)]);
        hasher.AppendData([(byte)(facts.SwitchTransitPathProven ? 1 : 0)]);
        hasher.AppendData([(byte)(facts.Candidate.HasForward ? 1 : 0)]);
        hasher.AppendData([(byte)(facts.Candidate.HasDstNatMatcher ? 1 : 0)]);
        hasher.AppendData([(byte)(facts.Candidate.HasStatefulConnectionMatcher ? 1 : 0)]);
        hasher.AppendData([(byte)(facts.Candidate.HandlesUntracked ? 1 : 0)]);
        hasher.AppendData([(byte)(facts.Candidate.DropsInvalid ? 1 : 0)]);
        hasher.AppendData([(byte)1]);

        foreach (UplinkCoverageFact uplink in facts.Uplinks.OrderBy(static u => u.Key, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, uplink.Key);
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(int)uplink.Mode]);
            AppendUtf8(hasher, uplink.ZoneKey ?? string.Empty);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        foreach (string id in facts.DeclaredVrrpMemberIds.OrderBy(static s => s, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, id);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        foreach (string id in facts.ObservedVrrpMemberIds.OrderBy(static s => s, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, id);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        foreach (VrrpInstanceFacts instance in facts.VrrpInstances
                     .OrderBy(static i => i.Family)
                     .ThenBy(static i => i.Vrid)
                     .ThenBy(static i => i.ParentInterface, StringComparer.Ordinal))
        {
            hasher.AppendData([(byte)(int)instance.Family]);
            hasher.AppendData([instance.Vrid]);
            AppendUtf8(hasher, instance.ParentInterface);
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(instance.Disabled ? 1 : 0)]);
            hasher.AppendData([(byte)(instance.SyncConnectionTracking ? 1 : 0)]);
            AppendUtf8(hasher, instance.SyncPort.ToString(CultureInfo.InvariantCulture));
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, instance.RemoteAddress ?? string.Empty);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        foreach (RoutingTableFact table in facts.RoutingTables.OrderBy(static t => t.Name, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, table.Name);
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(table.Disabled ? 1 : 0)]);
        }

        hasher.AppendData([(byte)1]);
        foreach (RoutingRuleFact rule in facts.RoutingRules.OrderBy(static r => r.Ordinal))
        {
            AppendUtf8(hasher, rule.Ordinal.ToString(CultureInfo.InvariantCulture));
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Action ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Table ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.RoutingMark ?? string.Empty);
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(rule.Disabled ? 1 : 0)]);
        }

        hasher.AppendData([(byte)1]);
        AppendFacility(hasher, facts.RawRules);
        AppendFacility(hasher, facts.NatRules);
        AppendFacility(hasher, facts.MangleRules);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>SHA-256 of operational observations. Must not enter policy hash or topology context hash.</summary>
    public static Hash256 HashTopologyObservation(TopologyDependencyFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, TopologyObservationPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        foreach (VrrpRoleAssignment role in facts.RoleVector
                     .OrderBy(static r => r.Family)
                     .ThenBy(static r => r.Vrid)
                     .ThenBy(static r => r.ParentInterface, StringComparer.Ordinal)
                     .ThenBy(static r => r.DeviceId, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, role.DeviceId);
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(int)role.Family]);
            hasher.AppendData([role.Vrid]);
            AppendUtf8(hasher, role.ParentInterface);
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(int)role.Role]);
        }

        hasher.AppendData([(byte)1]);
        foreach (DefaultRouteObservation route in facts.DefaultRouteObservations
                     .OrderBy(static r => r.Family)
                     .ThenBy(static r => r.Table ?? string.Empty, StringComparer.Ordinal)
                     .ThenBy(static r => r.Gateway ?? string.Empty, StringComparer.Ordinal))
        {
            hasher.AppendData([(byte)(int)route.Family]);
            AppendUtf8(hasher, route.Table ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, route.Gateway ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, route.Active ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, route.GatewayStatus ?? string.Empty);
            hasher.AppendData([(byte)0]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// analysis_context_hash that includes the M2-12, N1-04, M2-13 slots plus this topology-dependency slot.
    /// Does not change the one-, two-, or three-argument combiners.
    /// </summary>
    public static Hash256 HashAnalysisContext(
        Hash256 actualFilterContextHash,
        Hash256 packetPathContextHash,
        Hash256 managementPathContextHash,
        Hash256 topologyDependencyContextHash)
    {
        ArgumentNullException.ThrowIfNull(actualFilterContextHash);
        ArgumentNullException.ThrowIfNull(packetPathContextHash);
        ArgumentNullException.ThrowIfNull(managementPathContextHash);
        ArgumentNullException.ThrowIfNull(topologyDependencyContextHash);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, AnalysisContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, ActualFilterAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(actualFilterContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, PacketPathAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(packetPathContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, ManagementPathAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(managementPathContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(topologyDependencyContextHash.Bytes);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static void CheckVrrp(
        TopologyDependencyFacts facts,
        List<TopologyDependencyFinding> findings,
        List<ProtectedVrrpFlow> flows)
    {
        foreach (VrrpInstanceFacts instance in facts.VrrpInstances.Where(static i => !i.Disabled))
        {
            string destination = instance.Family == IpAddressFamily.IPv6
                ? Ipv6VrrpMulticast
                : Ipv4VrrpMulticast;
            flows.Add(Advertisement(instance, PolicyFilterChain.Input, destination));
            flows.Add(Advertisement(instance, PolicyFilterChain.Output, destination));
            if (!instance.SyncConnectionTracking)
            {
                continue;
            }

            string remote = instance.RemoteAddress ?? SyncMembersPlaceholder;
            flows.Add(SyncFlow(instance, PolicyFilterChain.Input, remote));
            flows.Add(SyncFlow(instance, PolicyFilterChain.Output, remote));
        }

        if (facts.DeclaredVrrpMemberIds.Count == 0)
        {
            return;
        }

        HashSet<string> observed = new(facts.ObservedVrrpMemberIds, StringComparer.Ordinal);
        foreach (string declared in facts.DeclaredVrrpMemberIds)
        {
            if (observed.Contains(declared))
            {
                continue;
            }

            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.VrrpMemberMissing,
                $"Declared VRRP member '{declared}' is missing from observed membership.",
                declared));
        }
    }

    private static void CheckUplinkCoverage(TopologyDependencyFacts facts, List<TopologyDependencyFinding> findings)
    {
        foreach (UplinkCoverageFact uplink in facts.Uplinks)
        {
            if (!string.IsNullOrWhiteSpace(uplink.ZoneKey))
            {
                continue;
            }

            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.UplinkZoneCoverageMissing,
                $"Uplink '{uplink.Key}' has no zone binding.",
                uplink.Key));
        }

        if (facts.UplinkMode is DeclaredUplinkMode.None or DeclaredUplinkMode.One)
        {
            return;
        }

        bool hasPrimary = facts.Uplinks.Any(static u => u.Mode == UplinkTrafficMode.Primary);
        bool hasBackup = facts.Uplinks.Any(static u => u.Mode == UplinkTrafficMode.Backup);
        bool hasBalanced = facts.Uplinks.Any(static u => u.Mode == UplinkTrafficMode.Balanced);
        if (facts.UplinkMode is DeclaredUplinkMode.Failover or DeclaredUplinkMode.Mixed)
        {
            if (!hasPrimary)
            {
                findings.Add(Finding(
                    TopologyDependencyAnalysisCodes.UplinkZoneCoverageMissing,
                    "Declared failover/mixed mode requires a primary uplink with zone coverage.",
                    "primary"));
            }

            if (!hasBackup)
            {
                findings.Add(Finding(
                    TopologyDependencyAnalysisCodes.UplinkZoneCoverageMissing,
                    "Declared failover/mixed mode requires a backup uplink with zone coverage.",
                    "backup"));
            }
        }

        if (facts.UplinkMode is DeclaredUplinkMode.Balanced or DeclaredUplinkMode.Mixed && !hasBalanced)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.UplinkZoneCoverageMissing,
                "Declared balanced/mixed mode requires a balanced uplink with zone coverage.",
                "balanced"));
        }
    }

    private static void CheckStrictRpf(TopologyDependencyFacts facts, List<TopologyDependencyFinding> findings)
    {
        if (!IsStrictRpFilter(facts.RpFilter))
        {
            return;
        }

        bool extraTables = HasNonMainRoutingTable(facts);
        bool vrrp = facts.VrrpInstances.Any(static i => !i.Disabled);
        bool asymmetric = HasAsymmetricEvidence(facts);
        if (extraTables)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.StrictRpfWithRoutingTables,
                "Strict rp-filter is incompatible with non-main routing tables.",
                "rp-filter"));
        }

        if (vrrp)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.StrictRpfWithVrrp,
                "Strict rp-filter is incompatible with VRRP without an approved infrastructure exception.",
                "rp-filter"));
        }

        if (asymmetric)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.StrictRpfWithAsymmetricRouting,
                "Strict rp-filter is incompatible with asymmetric or balanced routing.",
                "rp-filter"));
        }
    }

    private static void CheckInvalidDrop(TopologyDependencyFacts facts, List<TopologyDependencyFinding> findings)
    {
        if (!facts.Candidate.DropsInvalid || !HasAsymmetricEvidence(facts))
        {
            return;
        }

        findings.Add(Finding(
            TopologyDependencyAnalysisCodes.InvalidDropWithAsymmetricRouting,
            "Drop of connection-state=invalid intersects asymmetric or balanced forwarding paths.",
            "invalid"));
    }

    private static void CheckRaw(TopologyDependencyFacts facts, List<TopologyDependencyFinding> findings)
    {
        IReadOnlyList<FacilityRuleFact> enabled = Enabled(facts.RawRules);
        if (enabled.Any(static r => r.UnsupportedMatchers.Count > 0))
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.RawDependencyIndeterminate,
                "RAW has an unknown or unsupported matcher; notrack intersection cannot be proven.",
                "raw"));
        }

        bool notrack = enabled.Any(IsNotrack);
        if (!notrack)
        {
            return;
        }

        if (facts.Candidate.HasStatefulConnectionMatcher)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.RawNotrackIntersectsStateful,
                "RAW notrack intersects a stateful filter matcher; connection-tracking semantics are not proven.",
                "raw"));
        }

        if (!facts.Candidate.HandlesUntracked)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.RawNotrackTrafficNotHandled,
                "RAW notrack traffic is not handled as UNTRACKED on the candidate filter.",
                "raw"));
        }
    }

    private static void CheckNat(TopologyDependencyFacts facts, List<TopologyDependencyFinding> findings)
    {
        IReadOnlyList<FacilityRuleFact> enabled = Enabled(facts.NatRules);
        if (enabled.Any(static r => r.UnsupportedMatchers.Count > 0))
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.NatDependencyIndeterminate,
                "NAT has an unknown matcher; DSTNAT dependency cannot be proven.",
                "nat"));
        }

        if (!facts.Candidate.HasDstNatMatcher)
        {
            return;
        }

        bool dstNatEvidence = enabled.Any(static r =>
            string.Equals(r.Chain, "dstnat", StringComparison.OrdinalIgnoreCase));
        if (!dstNatEvidence)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.DstNatMatchWithoutNatEvidence,
                "Candidate uses connection-nat-state=dstnat but no dstnat NAT rule is present.",
                "nat",
                TopologyDependencyAnalysisCodes.SeverityWarning));
        }
    }

    private static void CheckMangle(TopologyDependencyFacts facts, List<TopologyDependencyFinding> findings)
    {
        IReadOnlyList<FacilityRuleFact> enabled = Enabled(facts.MangleRules);
        if (enabled.Any(static r => r.UnsupportedMatchers.Count > 0
                                    && r.UnsupportedMatchers.Any(static m =>
                                        !string.Equals(m, "per-connection-classifier", StringComparison.Ordinal))))
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.MangleAnalysisIndeterminate,
                "Mangle has an unknown matcher; mark and PCC dependencies cannot be proven.",
                "mangle"));
        }

        if (enabled.Any(static r => !string.IsNullOrWhiteSpace(r.PerConnectionClassifier)
                                    || r.UnsupportedMatchers.Contains("per-connection-classifier", StringComparer.Ordinal)))
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.ManglePccPresent,
                "Mangle per-connection-classifier (PCC) is present.",
                "mangle",
                TopologyDependencyAnalysisCodes.SeverityWarning));
        }

        bool routingMarks = enabled.Any(static r =>
                                 !string.IsNullOrWhiteSpace(r.RoutingMark)
                                 || !string.IsNullOrWhiteSpace(r.NewRoutingMark))
                             || Enabled(facts.RoutingRules).Any(static r => !string.IsNullOrWhiteSpace(r.RoutingMark));
        if (routingMarks)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.MangleRoutingMarkPresent,
                "Routing marks are present on Mangle or routing rules.",
                "mangle",
                TopologyDependencyAnalysisCodes.SeverityWarning));
        }
    }

    private static void CheckSwitch(TopologyDependencyFacts facts, List<TopologyDependencyFinding> findings)
    {
        if (facts.Kind != NodeKind.Switch)
        {
            return;
        }

        findings.Add(Finding(
            TopologyDependencyAnalysisCodes.SwitchForwardPolicyUnsupported,
            "SWITCH nodes cannot carry managed FORWARD policy in v1.",
            "forward"));

        if (!facts.SwitchHardwareProfileKnown)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.SwitchHardwareProfileUnknown,
                "SWITCH hardware chip profile is unknown; transit path cannot be proven.",
                "switch"));
        }

        if (!facts.SwitchTransitPathProven)
        {
            findings.Add(Finding(
                TopologyDependencyAnalysisCodes.SwitchTransitPathNotProven,
                "SWITCH transit path through the IP firewall is not proven.",
                "switch"));
        }
    }

    private static bool HasAsymmetricEvidence(TopologyDependencyFacts facts)
        => facts.UplinkMode is DeclaredUplinkMode.Balanced or DeclaredUplinkMode.Mixed
           || facts.Uplinks.Any(static u => u.Mode == UplinkTrafficMode.Balanced)
           || (facts.UplinkMode == DeclaredUplinkMode.Failover && HasNonMainRoutingTable(facts));

    private static bool HasNonMainRoutingTable(TopologyDependencyFacts facts)
        => facts.RoutingTables.Any(static t =>
               !t.Disabled && !string.Equals(t.Name, "main", StringComparison.OrdinalIgnoreCase))
           || Enabled(facts.RoutingRules).Any(static r =>
               !string.IsNullOrWhiteSpace(r.Table)
               && !string.Equals(r.Table, "main", StringComparison.OrdinalIgnoreCase));

    private static bool IsStrictRpFilter(string? value)
        => string.Equals(value, "strict", StringComparison.OrdinalIgnoreCase);

    private static bool IsNotrack(FacilityRuleFact rule)
        => string.Equals(rule.Action, "notrack", StringComparison.OrdinalIgnoreCase);

    private static FacilityRuleFact[] Enabled(IReadOnlyList<FacilityRuleFact> rules)
        => rules.Where(static r => !r.Disabled).ToArray();

    private static RoutingRuleFact[] Enabled(IReadOnlyList<RoutingRuleFact> rules)
        => rules.Where(static r => !r.Disabled).ToArray();

    private static ProtectedVrrpFlow Advertisement(
        VrrpInstanceFacts instance,
        PolicyFilterChain chain,
        string destination)
        => new()
        {
            Family = instance.Family,
            Chain = chain,
            Protocol = VrrpProtocol,
            Destination = destination,
            HopLimitOrTtl = 255,
            Interface = instance.ParentInterface,
            Kind = VrrpProtectedFlowKind.Advertisement,
        };

    private static ProtectedVrrpFlow SyncFlow(
        VrrpInstanceFacts instance,
        PolicyFilterChain chain,
        string remote)
        => new()
        {
            Family = instance.Family,
            Chain = chain,
            Protocol = IpProtocol.Udp,
            Destination = remote,
            Interface = instance.ParentInterface,
            Kind = VrrpProtectedFlowKind.Sync,
            DestinationPort = instance.SyncPort,
            RemoteAddress = instance.RemoteAddress,
        };

    private static TopologyDependencyFinding Finding(
        string code,
        string message,
        string? subject,
        string? severity = null)
        => new()
        {
            Code = code,
            Severity = severity ?? TopologyDependencyAnalysisCodes.SeverityBlocker,
            Message = message,
            Subject = subject,
        };

    private static void AppendFacility(IncrementalHash hasher, IReadOnlyList<FacilityRuleFact> rules)
    {
        foreach (FacilityRuleFact rule in rules
                     .OrderBy(static r => r.Family)
                     .ThenBy(static r => r.Ordinal))
        {
            hasher.AppendData([(byte)(int)rule.Family]);
            AppendUtf8(hasher, rule.Ordinal.ToString(CultureInfo.InvariantCulture));
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Chain ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Action ?? string.Empty);
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(rule.Disabled ? 1 : 0)]);
            AppendUtf8(hasher, rule.RoutingMark ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.NewRoutingMark ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.PerConnectionClassifier ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.ConnectionMark ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.PacketMark ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.NewConnectionMark ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.NewPacketMark ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.ToAddresses ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.ToPorts ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.ConnectionState ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.ConnectionNatState ?? string.Empty);
            hasher.AppendData([(byte)0]);
            foreach (string matcher in rule.UnsupportedMatchers.OrderBy(static m => m, StringComparer.Ordinal))
            {
                AppendUtf8(hasher, matcher);
                hasher.AppendData([(byte)0]);
            }

            hasher.AppendData([(byte)2]);
        }

        hasher.AppendData([(byte)1]);
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));
}
