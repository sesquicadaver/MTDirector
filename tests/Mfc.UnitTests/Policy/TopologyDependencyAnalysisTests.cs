using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class TopologyDependencyAnalysisTests
{
    [Fact]
    public void Ac1VrrpProtocol112AdvertisementFlowsAreGenerated()
    {
        TopologyDependencyAnalysisResult result = Analyze(VrrpOnly(IpAddressFamily.IPv4));
        Assert.Equal(2, result.ProtectedFlows.Count);
        Assert.All(result.ProtectedFlows, f => Assert.Equal(TopologyDependencyAnalysis.VrrpProtocol, f.Protocol));
        Assert.Contains(result.ProtectedFlows, f => f.Chain == PolicyFilterChain.Input);
        Assert.Contains(result.ProtectedFlows, f => f.Chain == PolicyFilterChain.Output);
        Assert.All(
            result.ProtectedFlows,
            f => Assert.Equal(VrrpProtectedFlowKind.Advertisement, f.Kind));
    }

    [Fact]
    public void Ac2Ipv4AndIpv6MulticastDestinationsAreChecked()
    {
        TopologyDependencyFacts facts = TopologyDependencyFacts.Create(
            vrrpInstances:
            [
                VrrpInstanceFacts.Create(IpAddressFamily.IPv4, 10, "ether1"),
                VrrpInstanceFacts.Create(IpAddressFamily.IPv6, 10, "bridge1"),
            ]);
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(facts);
        Assert.Contains(
            result.ProtectedFlows,
            f => f.Family == IpAddressFamily.IPv4
                 && f.Destination == TopologyDependencyAnalysis.Ipv4VrrpMulticast
                 && f.HopLimitOrTtl == 255);
        Assert.Contains(
            result.ProtectedFlows,
            f => f.Family == IpAddressFamily.IPv6
                 && f.Destination == TopologyDependencyAnalysis.Ipv6VrrpMulticast
                 && f.HopLimitOrTtl == 255);
        Assert.Equal(4, result.ProtectedFlows.Count(f => f.Kind == VrrpProtectedFlowKind.Advertisement));
    }

    [Fact]
    public void Ac3VrrpConnectionTrackingSyncFlowsUseConfiguredUdpPort()
    {
        TopologyDependencyFacts facts = TopologyDependencyFacts.Create(
            vrrpInstances:
            [
                VrrpInstanceFacts.Create(
                    IpAddressFamily.IPv4,
                    1,
                    "ether1",
                    syncConnectionTracking: true,
                    remoteAddress: "192.0.2.20"),
            ]);
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(facts);
        Assert.Contains(
            result.ProtectedFlows,
            f => f.Kind == VrrpProtectedFlowKind.Sync
                 && f.Protocol == IpProtocol.Udp
                 && f.DestinationPort == TopologyDependencyAnalysis.DefaultVrrpSyncPort
                 && f.Destination == "192.0.2.20"
                 && f.Chain == PolicyFilterChain.Input);
        Assert.Contains(
            result.ProtectedFlows,
            f => f.Kind == VrrpProtectedFlowKind.Sync && f.Chain == PolicyFilterChain.Output);
    }

    [Fact]
    public void Ac4MissingVrrpMemberIsBlocker()
    {
        TopologyDependencyFacts facts = TopologyDependencyFacts.Create(
            vrrpInstances: [VrrpInstanceFacts.Create(IpAddressFamily.IPv4, 1, "ether1")],
            declaredVrrpMemberIds: ["device-a", "device-b"],
            observedVrrpMemberIds: ["device-a"]);
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(facts);
        TopologyDependencyFinding finding = Assert.Single(
            result.Findings,
            f => f.Code == TopologyDependencyAnalysisCodes.VrrpMemberMissing);
        Assert.Equal(TopologyDependencyAnalysisCodes.SeverityBlocker, finding.Severity);
        Assert.Equal("device-b", finding.Subject);
        Assert.True(result.HasBlockers);
        Assert.True(TopologyDependencyAnalysisCodes.IsFailedPrecondition(finding.Code));
    }

    [Fact]
    public void Ac5SplitMasterRoleVectorIsPreserved()
    {
        TopologyDependencyFacts facts = TopologyDependencyFacts.Create(
            vrrpInstances:
            [
                VrrpInstanceFacts.Create(IpAddressFamily.IPv4, 10, "ether1"),
                VrrpInstanceFacts.Create(IpAddressFamily.IPv4, 20, "ether2"),
            ],
            roleVector:
            [
                VrrpRoleAssignment.Create("a", IpAddressFamily.IPv4, 10, "ether1", VrrpMemberObservedState.Master),
                VrrpRoleAssignment.Create("a", IpAddressFamily.IPv4, 20, "ether2", VrrpMemberObservedState.Backup),
            ]);
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(facts);
        Assert.Equal(2, result.RoleVector.Count);
        Assert.Contains(result.RoleVector, r => r.Vrid == 10 && r.Role == VrrpMemberObservedState.Master);
        Assert.Contains(result.RoleVector, r => r.Vrid == 20 && r.Role == VrrpMemberObservedState.Backup);
        Assert.False(result.HasCollapsedGlobalMaster);
        Assert.DoesNotContain(result.Findings, f => f.Code.Contains("SPLIT", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac6AllUplinksMustHaveZoneCoverage()
    {
        TopologyDependencyFacts facts = TopologyDependencyFacts.Create(
            uplinkMode: DeclaredUplinkMode.Failover,
            uplinks:
            [
                UplinkCoverageFact.Create("wan1", UplinkTrafficMode.Primary, "wan"),
                UplinkCoverageFact.Create("wan2", UplinkTrafficMode.Backup, zoneKey: null),
            ]);
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(facts);
        Assert.Contains(
            result.Findings,
            f => f.Code == TopologyDependencyAnalysisCodes.UplinkZoneCoverageMissing && f.Subject == "wan2");
    }

    [Fact]
    public void Ac7RoutingTablesAndRulesEnterContextHash()
    {
        TopologyDependencyFacts baseFacts = TopologyDependencyFacts.Create(
            routingTables: [RoutingTableFact.Create("main")]);
        TopologyDependencyFacts withTable = TopologyDependencyFacts.Create(
            routingTables: [RoutingTableFact.Create("main"), RoutingTableFact.Create("wan2")],
            routingRules: [RoutingRuleFact.Create(0, "lookup", "wan2", "mark-wan2")]);
        Hash256 without = TopologyDependencyAnalysis.HashTopologyDependencyContext(baseFacts);
        Hash256 with = TopologyDependencyAnalysis.HashTopologyDependencyContext(withTable);
        Assert.NotEqual(without.ToString(), with.ToString());
        TopologyDependencyAnalysisResult analyzed = TopologyDependencyAnalysis.Analyze(withTable);
        Assert.Equal(with.ToString(), analyzed.TopologyDependencyContextHash.ToString());
    }

    [Fact]
    public void Ac8PccAndRoutingMarksAreDetectedAsWarnings()
    {
        TopologyDependencyFacts facts = TopologyDependencyFacts.Create(
            mangleRules:
            [
                FacilityRuleFact.Create(
                    IpAddressFamily.IPv4,
                    0,
                    "prerouting",
                    "mark-routing",
                    perConnectionClassifier: "both-addresses:2/0",
                    newRoutingMark: "wan1",
                    unsupportedMatchers: ["per-connection-classifier"]),
            ]);
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(facts);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.ManglePccPresent);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.MangleRoutingMarkPresent);
        Assert.All(
            result.Findings.Where(f =>
                f.Code is TopologyDependencyAnalysisCodes.ManglePccPresent
                    or TopologyDependencyAnalysisCodes.MangleRoutingMarkPresent),
            f => Assert.Equal(TopologyDependencyAnalysisCodes.SeverityWarning, f.Severity));
        Assert.False(TopologyDependencyAnalysisCodes.IsFailedPrecondition(TopologyDependencyAnalysisCodes.ManglePccPresent));
        Assert.False(result.HasBlockers);
    }

    [Fact]
    public void Ac9StrictRpFilterWithVrrpAndAsymmetryIsBlocked()
    {
        TopologyDependencyFacts facts = TopologyDependencyFacts.Create(
            uplinkMode: DeclaredUplinkMode.Balanced,
            uplinks:
            [
                UplinkCoverageFact.Create("wan1", UplinkTrafficMode.Balanced, "wan1"),
                UplinkCoverageFact.Create("wan2", UplinkTrafficMode.Balanced, "wan2"),
            ],
            vrrpInstances: [VrrpInstanceFacts.Create(IpAddressFamily.IPv4, 1, "ether1")],
            routingTables: [RoutingTableFact.Create("main"), RoutingTableFact.Create("wan2")],
            rpFilter: "strict");
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(facts);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.StrictRpfWithVrrp);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.StrictRpfWithRoutingTables);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.StrictRpfWithAsymmetricRouting);
        Assert.True(result.HasBlockers);
    }

    [Fact]
    public void Ac10RawNotrackIntersectionIsAnalyzed()
    {
        TopologyDependencyFacts intersect = TopologyDependencyFacts.Create(
            rawRules: [FacilityRuleFact.Create(IpAddressFamily.IPv4, 0, "prerouting", "notrack")],
            candidate: CandidatePolicySurface.Create(hasStatefulConnectionMatcher: true));
        TopologyDependencyAnalysisResult blocked = TopologyDependencyAnalysis.Analyze(intersect);
        Assert.Contains(blocked.Findings, f => f.Code == TopologyDependencyAnalysisCodes.RawNotrackIntersectsStateful);
        Assert.Contains(blocked.Findings, f => f.Code == TopologyDependencyAnalysisCodes.RawNotrackTrafficNotHandled);

        TopologyDependencyFacts unknown = TopologyDependencyFacts.Create(
            rawRules:
            [
                FacilityRuleFact.Create(
                    IpAddressFamily.IPv4,
                    0,
                    "prerouting",
                    "notrack",
                    unsupportedMatchers: ["nth"]),
            ]);
        TopologyDependencyAnalysisResult indeterminate = TopologyDependencyAnalysis.Analyze(unknown);
        Assert.Contains(
            indeterminate.Findings,
            f => f.Code == TopologyDependencyAnalysisCodes.RawDependencyIndeterminate);
    }

    [Fact]
    public void Ac11DstNatDependenciesAreAnalyzed()
    {
        TopologyDependencyFacts missing = TopologyDependencyFacts.Create(
            candidate: CandidatePolicySurface.Create(hasDstNatMatcher: true));
        TopologyDependencyAnalysisResult warning = TopologyDependencyAnalysis.Analyze(missing);
        TopologyDependencyFinding finding = Assert.Single(
            warning.Findings,
            f => f.Code == TopologyDependencyAnalysisCodes.DstNatMatchWithoutNatEvidence);
        Assert.Equal(TopologyDependencyAnalysisCodes.SeverityWarning, finding.Severity);
        Assert.False(TopologyDependencyAnalysisCodes.IsFailedPrecondition(finding.Code));

        TopologyDependencyFacts unknown = TopologyDependencyFacts.Create(
            natRules:
            [
                FacilityRuleFact.Create(
                    IpAddressFamily.IPv4,
                    0,
                    "dstnat",
                    "dst-nat",
                    unsupportedMatchers: ["nth"]),
            ],
            candidate: CandidatePolicySurface.Create(hasDstNatMatcher: true));
        TopologyDependencyAnalysisResult indeterminate = TopologyDependencyAnalysis.Analyze(unknown);
        Assert.Contains(
            indeterminate.Findings,
            f => f.Code == TopologyDependencyAnalysisCodes.NatDependencyIndeterminate);
        Assert.DoesNotContain(
            indeterminate.Findings,
            f => f.Code == TopologyDependencyAnalysisCodes.DstNatMatchWithoutNatEvidence);
    }

    [Fact]
    public void Ac12MangleDependencyHashEntersAnalysisContext()
    {
        TopologyDependencyFacts empty = TopologyDependencyFacts.Create();
        TopologyDependencyFacts withMangle = TopologyDependencyFacts.Create(
            mangleRules: [FacilityRuleFact.Create(IpAddressFamily.IPv4, 0, "prerouting", "mark-connection")]);
        Hash256 without = TopologyDependencyAnalysis.HashTopologyDependencyContext(empty);
        Hash256 with = TopologyDependencyAnalysis.HashTopologyDependencyContext(withMangle);
        Assert.NotEqual(without.ToString(), with.ToString());
        TopologyDependencyFacts markOnly = TopologyDependencyFacts.Create(
            mangleRules:
            [
                FacilityRuleFact.Create(
                    IpAddressFamily.IPv4,
                    0,
                    "prerouting",
                    "mark-connection",
                    connectionMark: "c1"),
            ]);
        Assert.NotEqual(
            with.ToString(),
            TopologyDependencyAnalysis.HashTopologyDependencyContext(markOnly).ToString());

        Hash256 actual = ActualFilterAnalysis.HashActualContext([]);
        Hash256 packet = PacketPathAnalysis.HashPacketPathContext([]);
        Hash256 management = ManagementPathAnalysis.HashManagementPathContext(
            ManagementAccessProfile.Create([AddressPrefix.Parse("192.0.2.0/24")], "192.0.2.10", 8729),
            ManagementIpServiceFacts.Create(true, false, "8729", null),
            []);
        Hash256 combined = TopologyDependencyAnalysis.HashAnalysisContext(actual, packet, management, with);
        Assert.NotEqual(
            ManagementPathAnalysis.HashAnalysisContext(actual, packet, management).ToString(),
            combined.ToString());
        Assert.Equal(
            combined.ToString(),
            TopologyDependencyAnalysis.HashAnalysisContext(actual, packet, management, with).ToString());
    }

    [Fact]
    public void Ac13SwitchForwardPolicyIsBlocked()
    {
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(
            TopologyDependencyFacts.Create(kind: NodeKind.Switch));
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.SwitchForwardPolicyUnsupported);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.SwitchHardwareProfileUnknown);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.SwitchTransitPathNotProven);
        Assert.True(result.HasBlockers);
    }

    [Fact]
    public void Ac14OperationalRouteOrVrrpRoleDoesNotChangeContextHash()
    {
        VrrpInstanceFacts instance = VrrpInstanceFacts.Create(IpAddressFamily.IPv4, 1, "ether1");
        TopologyDependencyFacts master = TopologyDependencyFacts.Create(
            vrrpInstances: [instance],
            roleVector:
            [
                VrrpRoleAssignment.Create("a", IpAddressFamily.IPv4, 1, "ether1", VrrpMemberObservedState.Master),
            ],
            defaultRouteObservations:
            [
                DefaultRouteObservation.Create(IpAddressFamily.IPv4, "main", "1.1.1.1", "true", "reachable"),
            ]);
        TopologyDependencyFacts backup = TopologyDependencyFacts.Create(
            vrrpInstances: [instance],
            roleVector:
            [
                VrrpRoleAssignment.Create("a", IpAddressFamily.IPv4, 1, "ether1", VrrpMemberObservedState.Backup),
            ],
            defaultRouteObservations:
            [
                DefaultRouteObservation.Create(IpAddressFamily.IPv4, "main", "9.9.9.9", "false", "unreachable"),
            ]);
        TopologyDependencyAnalysisResult first = TopologyDependencyAnalysis.Analyze(master);
        TopologyDependencyAnalysisResult second = TopologyDependencyAnalysis.Analyze(backup);
        Assert.Equal(first.TopologyDependencyContextHash.ToString(), second.TopologyDependencyContextHash.ToString());
        Assert.NotEqual(first.TopologyObservationHash.ToString(), second.TopologyObservationHash.ToString());

        TopologyDependencyFacts syncChanged = TopologyDependencyFacts.Create(
            vrrpInstances:
            [
                VrrpInstanceFacts.Create(IpAddressFamily.IPv4, 1, "ether1", syncConnectionTracking: true),
            ],
            roleVector: master.RoleVector,
            defaultRouteObservations: master.DefaultRouteObservations);
        Assert.NotEqual(
            first.TopologyDependencyContextHash.ToString(),
            TopologyDependencyAnalysis.Analyze(syncChanged).TopologyDependencyContextHash.ToString());
    }

    [Fact]
    public void InvalidDropWithAsymmetricRoutingIsBlocker()
    {
        TopologyDependencyFacts facts = TopologyDependencyFacts.Create(
            uplinkMode: DeclaredUplinkMode.Balanced,
            uplinks: [UplinkCoverageFact.Create("wan1", UplinkTrafficMode.Balanced, "wan")],
            candidate: CandidatePolicySurface.Create(dropsInvalid: true));
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(facts);
        Assert.Contains(
            result.Findings,
            f => f.Code == TopologyDependencyAnalysisCodes.InvalidDropWithAsymmetricRouting);
    }

    [Fact]
    public void UnknownMangleMatcherIsIndeterminateBlocker()
    {
        TopologyDependencyFacts facts = TopologyDependencyFacts.Create(
            mangleRules:
            [
                FacilityRuleFact.Create(
                    IpAddressFamily.IPv4,
                    0,
                    "prerouting",
                    "mark-routing",
                    unsupportedMatchers: ["nth"]),
            ]);
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(facts);
        Assert.Contains(result.Findings, f => f.Code == TopologyDependencyAnalysisCodes.MangleAnalysisIndeterminate);
        Assert.True(TopologyDependencyAnalysisCodes.IsFailedPrecondition(
            TopologyDependencyAnalysisCodes.MangleAnalysisIndeterminate));
    }

    [Fact]
    public void CodeAndFactInvariantsHold()
    {
        Assert.False(TopologyDependencyAnalysisCodes.IsFailedPrecondition(string.Empty));
        Assert.False(TopologyDependencyAnalysisCodes.IsFailedPrecondition(
            TopologyDependencyAnalysisCodes.MangleRoutingMarkPresent));
        Assert.True(TopologyDependencyAnalysisCodes.IsFailedPrecondition(
            TopologyDependencyAnalysisCodes.VrrpMemberMissing));
        Assert.Throws<DomainInvariantException>(() => VrrpInstanceFacts.Create(IpAddressFamily.IPv4, 0, "ether1"));
        Assert.Throws<DomainInvariantException>(() =>
            UplinkCoverageFact.Create(" ", UplinkTrafficMode.Primary, "wan"));
        Assert.Equal(112, IpProtocol.Vrrp);
    }

    [Fact]
    public void DisabledVrrpInstanceDoesNotEmitFlows()
    {
        TopologyDependencyAnalysisResult result = TopologyDependencyAnalysis.Analyze(
            TopologyDependencyFacts.Create(
                vrrpInstances: [VrrpInstanceFacts.Create(IpAddressFamily.IPv4, 1, "ether1", disabled: true)]));
        Assert.Empty(result.ProtectedFlows);
    }

    private static TopologyDependencyAnalysisResult Analyze(TopologyDependencyFacts facts)
        => TopologyDependencyAnalysis.Analyze(facts);

    private static TopologyDependencyFacts VrrpOnly(IpAddressFamily family)
        => TopologyDependencyFacts.Create(
            vrrpInstances: [VrrpInstanceFacts.Create(family, 1, "ether1")]);
}
