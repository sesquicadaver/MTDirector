using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class FastTrackAnalysisTests
{
    [Fact]
    public void Ac1AllowedOnlyOnIpv4Forward()
    {
        FastTrackAnalysisResult ipv6 = Analyze(Rule(family: IpAddressFamily.IPv6));
        Assert.Contains(ipv6.Findings, f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        FastTrackAnalysisResult input = Analyze(Rule(chain: PolicyFilterChain.Input));
        Assert.Contains(input.Findings, f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        FastTrackAnalysisResult output = Analyze(Rule(chain: PolicyFilterChain.Output));
        Assert.Contains(output.Findings, f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        FastTrackAnalysisResult allowed = AnalyzeAllowed();
        Assert.False(allowed.HasBlockers);
        Assert.True(allowed.AllowsSafeFastTrack);
    }

    [Fact]
    public void Ac2AllowedOnlyOnCompanyStatePrelude()
    {
        FastTrackAnalysisResult allowed = AnalyzeAllowed();
        Assert.All(
            allowed.Findings.Where(f => f.Code == FastTrackAnalysisCodes.ContextUnsupported),
            _ => Assert.Fail("STATE_PRELUDE FastTrack must not emit CONTEXT on the allowlist."));
        Assert.Equal(PolicyPipelineStage.StatePrelude, AllowedRule().Stage);
        Assert.Equal(PolicyOwnerScope.Company, PolicyPipelineV1.RequiredOwner(PolicyPipelineStage.StatePrelude));
    }

    [Fact]
    public void Ac3ConnectionStateMustBeEstablishedRelatedSubset()
    {
        Assert.Contains(
            Analyze(Rule(states: [ConnectionState.New, ConnectionState.Established])).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(
            Analyze(Rule(states: [ConnectionState.Untracked])).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(
            Analyze(Rule(unconstrainedStates: true)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        FastTrackAnalysisResult relatedOnly = Analyze(Rule(states: [ConnectionState.Related]));
        Assert.DoesNotContain(
            relatedOnly.Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported
                 && f.Message.Contains("connection-state", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac4ProtocolMustBeTcpOrUdpSubset()
    {
        (PolicyRule anyRule, Dictionary<ServiceObjectId, ServiceObject> empty) = AnyProtocolRule();
        Assert.Contains(
            FastTrackAnalysis.Analyze([anyRule], FastTrackTopologyContext.SafeSingleWan, empty).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        ServiceObject icmp = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("icmp"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Icmp, "icmp"))]);
        PolicyRule icmpRule = Rule(services: ServiceSelector.Create([icmp.Id]));
        Assert.Contains(
            FastTrackAnalysis.Analyze(
                [icmpRule],
                FastTrackTopologyContext.SafeSingleWan,
                new Dictionary<ServiceObjectId, ServiceObject> { [icmp.Id] = icmp }).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        ServiceObjectId missing = ServiceObjectId.New();
        PolicyRule missingRule = Rule(services: ServiceSelector.Create([missing]));
        Assert.Contains(
            FastTrackAnalysis.Analyze([missingRule], FastTrackTopologyContext.SafeSingleWan).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        FastTrackAnalysisResult udp = AnalyzeAllowed(udpOnly: true);
        Assert.False(udp.HasBlockers);
    }

    [Fact]
    public void Ac5Ipv6FastTrackIsBlocked()
    {
        FastTrackAnalysisResult result = Analyze(Rule(family: IpAddressFamily.IPv6));
        Assert.Contains(result.Findings, f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.True(result.HasBlockers);
        Assert.True(FastTrackAnalysisCodes.IsFailedPrecondition(FastTrackAnalysisCodes.ContextUnsupported));
    }

    [Fact]
    public void Ac6PccAndBalancedMixedMultiWanBlockFastTrack()
    {
        Assert.Contains(
            Analyze(Rule(), FastTrackTopologyContext.Create(hasPcc: true)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(
            Analyze(Rule(), FastTrackTopologyContext.Create(DeclaredUplinkMode.Balanced)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(
            Analyze(Rule(), FastTrackTopologyContext.Create(DeclaredUplinkMode.Mixed)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        FastTrackAnalysisResult failoverMain = Analyze(
            Rule(),
            FastTrackTopologyContext.Create(DeclaredUplinkMode.Failover));
        Assert.False(failoverMain.HasBlockers);
    }

    [Fact]
    public void Ac7RoutingMarksAndNonMainTablesBlockFastTrack()
    {
        Assert.Contains(
            Analyze(Rule(), FastTrackTopologyContext.Create(hasRoutingMarks: true)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(
            Analyze(
                Rule(),
                FastTrackTopologyContext.Create(
                    DeclaredUplinkMode.Failover,
                    hasNonMainRoutingTables: true)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(
            Analyze(Rule(), FastTrackTopologyContext.Create(DeclaredUplinkMode.None)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
    }

    [Fact]
    public void Ac8IpsecVrfAndUnknownMangleBlockFastTrack()
    {
        PolicyRule ipsec = Rule(
            ipsec: IpsecPolicyPredicate.Create(IpsecDirection.In, IpsecPolicyKind.Ipsec));
        Assert.Contains(
            Analyze(ipsec).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(
            Analyze(Rule(), FastTrackTopologyContext.Create(hasVrf: true)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(
            Analyze(Rule(), FastTrackTopologyContext.Create(hasUnknownMangle: true)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.Contains(
            Analyze(
                Rule(),
                FastTrackTopologyContext.Create(hasPacketMarksRequiredAfterFastTrack: true)).Findings,
            f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
    }

    [Fact]
    public void Ac9PreAnchorUnmanagedFastTrackIsAccounted()
    {
        FastTrackAnalysisResult result = Analyze(
            Rule(),
            FastTrackTopologyContext.Create(hasPreAnchorUnmanagedFastTrack: true));
        Assert.Contains(
            result.Findings,
            f => f.Code == ActualFilterAnalysisCodes.PreAnchorFasttrackBypasses);
        Assert.True(FastTrackAnalysisCodes.IsFailedPrecondition(ActualFilterAnalysisCodes.PreAnchorFasttrackBypasses));
        Assert.True(
            FastTrackAnalysis.HasPreAnchorUnmanagedFastTrack(
            [
                ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "forward",
                    0,
                    "fasttrack-connection",
                    comment: "unmanaged"),
                ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "forward",
                    1,
                    "jump",
                    comment: "fwc:anchor:forward"),
            ]));
        Assert.False(
            FastTrackAnalysis.HasPreAnchorUnmanagedFastTrack(
            [
                ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "forward",
                    0,
                    "jump",
                    comment: "fwc:anchor:forward"),
                ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "forward",
                    1,
                    "fasttrack-connection",
                    comment: "unmanaged"),
            ]));
    }

    [Fact]
    public void Ac10FallbackAcceptIsMandatoryCompilerContract()
    {
        FastTrackAnalysisResult present = AnalyzeAllowed();
        Assert.True(present.RequiresAcceptFallback);
        Assert.Contains(present.Findings, f => f.Code == FastTrackAnalysisCodes.FallbackRequired);
        Assert.False(FastTrackAnalysisCodes.IsFailedPrecondition(FastTrackAnalysisCodes.FallbackRequired));
        FastTrackAnalysisResult absent = FastTrackAnalysis.Analyze(
            [
                PolicyRule.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    PolicyPipelineStage.StatePrelude,
                    0,
                    TrafficPredicate.Create(),
                    RuleEffectSpec.Create(PolicyRuleEffect.Accept)),
            ],
            FastTrackTopologyContext.SafeSingleWan);
        Assert.False(absent.RequiresAcceptFallback);
        Assert.Empty(absent.Findings);
        Assert.Null(absent.RiskFloor);
    }

    [Fact]
    public void Ac11FastTrackRiskIsNotBelowHigh()
    {
        FastTrackAnalysisResult allowed = AnalyzeAllowed();
        Assert.Equal(FastTrackAnalysisCodes.RiskHigh, allowed.RiskFloor);
        Assert.All(allowed.Findings, f => Assert.Equal(FastTrackAnalysisCodes.RiskHigh, f.Risk));
        FastTrackAnalysisResult blocked = Analyze(Rule(), FastTrackTopologyContext.Create(hasPcc: true));
        Assert.Equal(FastTrackAnalysisCodes.RiskHigh, blocked.RiskFloor);
        Assert.Contains(
            Analyze(Rule(), FastTrackTopologyContext.Create(hasHotSpot: true)).Findings,
            f => f.Code == FastTrackAnalysisCodes.CapabilityUnsupported);
        Assert.Contains(
            Analyze(Rule(logging: LogSpecification.Create(true, "ft"))).Findings,
            f => f.Code == FastTrackAnalysisCodes.LoggingUnsupported);
        Assert.Contains(
            Analyze(Rule(), FastTrackTopologyContext.Create(connectionTrackingPresent: false)).Findings,
            f => f.Code == FastTrackAnalysisCodes.CapabilityUnsupported);
        Assert.True(FastTrackAnalysisCodes.IsFailedPrecondition(FastTrackAnalysisCodes.LoggingUnsupported));
        Assert.True(FastTrackAnalysisCodes.IsFailedPrecondition(FastTrackAnalysisCodes.CapabilityUnsupported));
        Assert.False(FastTrackAnalysisCodes.IsFailedPrecondition(string.Empty));
    }

    [Fact]
    public void Ac12HashSlotIsIsolatedFromPriorCombiners()
    {
        FastTrackTopologyContext safe = FastTrackTopologyContext.SafeSingleWan;
        FastTrackTopologyContext pcc = FastTrackTopologyContext.Create(hasPcc: true);
        PolicyRule rule = AllowedRule();
        Assert.NotEqual(
            FastTrackAnalysis.HashFastTrackContext([rule], safe).ToString(),
            FastTrackAnalysis.HashFastTrackContext([rule], pcc).ToString());

        Hash256 actual = ActualFilterAnalysis.HashActualContext([]);
        Hash256 packet = PacketPathAnalysis.HashPacketPathContext([]);
        Hash256 management = ManagementPathAnalysis.HashManagementPathContext(
            ManagementAccessProfile.Create([AddressPrefix.Parse("192.0.2.0/24")], "192.0.2.10", 8729),
            ManagementIpServiceFacts.Create(true, false, "8729", null),
            []);
        Hash256 topology = TopologyDependencyAnalysis.HashTopologyDependencyContext(TopologyDependencyFacts.Create());
        Hash256 fast = FastTrackAnalysis.HashFastTrackContext([rule], safe);
        Hash256 four = TopologyDependencyAnalysis.HashAnalysisContext(actual, packet, management, topology);
        Hash256 five = FastTrackAnalysis.HashAnalysisContext(actual, packet, management, topology, fast);
        Assert.NotEqual(four.ToString(), five.ToString());
        Assert.Equal(
            four.ToString(),
            TopologyDependencyAnalysis.HashAnalysisContext(actual, packet, management, topology).ToString());
        Assert.Equal(
            five.ToString(),
            FastTrackAnalysis.HashAnalysisContext(actual, packet, management, topology, fast).ToString());

        FastTrackTopologyContext fromFacts = FastTrackTopologyContext.From(
            TopologyDependencyFacts.Create(
                uplinkMode: DeclaredUplinkMode.One,
                mangleRules:
                [
                    FacilityRuleFact.Create(
                        IpAddressFamily.IPv4,
                        0,
                        "prerouting",
                        "mark-routing",
                        perConnectionClassifier: "both-addresses:2/0",
                        newRoutingMark: "wan1",
                        packetMark: "p1",
                        unsupportedMatchers: ["nth"]),
                ],
                routingTables: [RoutingTableFact.Create("wan2")],
                routingRules: [RoutingRuleFact.Create(0, "lookup", "wan2", "wan2")]));
        Assert.True(fromFacts.HasPcc);
        Assert.True(fromFacts.HasRoutingMarks);
        Assert.True(fromFacts.HasNonMainRoutingTables);
        Assert.True(fromFacts.HasUnknownMangle);
        Assert.True(fromFacts.HasPacketMarksRequiredAfterFastTrack);
    }

    [Fact]
    public void DisabledFastTrackRulesAreStillValidated()
    {
        FastTrackAnalysisResult result = Analyze(Rule(enabled: false, family: IpAddressFamily.IPv6));
        Assert.Contains(result.Findings, f => f.Code == FastTrackAnalysisCodes.ContextUnsupported);
        Assert.True(result.RequiresAcceptFallback);
    }

    private static readonly ServiceObject Tcp = ServiceObject.Create(
        PolicyObjectOwnerScope.Company,
        null,
        null,
        NonEmptyName.Create("tcp"),
        [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "tcp"))]);

    private static readonly ServiceObject Udp = ServiceObject.Create(
        PolicyObjectOwnerScope.Company,
        null,
        null,
        NonEmptyName.Create("udp"),
        [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Udp, "udp"))]);

    private static readonly Dictionary<ServiceObjectId, ServiceObject> Catalog = new()
    {
        [Tcp.Id] = Tcp,
        [Udp.Id] = Udp,
    };

    private static FastTrackAnalysisResult AnalyzeAllowed(bool udpOnly = false)
        => Analyze(AllowedRule(udpOnly));

    private static FastTrackAnalysisResult Analyze(
        PolicyRule rule,
        FastTrackTopologyContext? topology = null)
        => FastTrackAnalysis.Analyze([rule], topology ?? FastTrackTopologyContext.SafeSingleWan, Catalog);

    private static PolicyRule AllowedRule(bool udpOnly = false)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            0,
            TrafficPredicate.Create(
                services: ServiceSelector.Create([udpOnly ? Udp.Id : Tcp.Id]),
                connectionStates: [ConnectionState.Established, ConnectionState.Related],
                serviceCatalog: Catalog),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept),
            LogSpecification.Disabled);

    private static PolicyRule Rule(
        IpAddressFamily family = IpAddressFamily.IPv4,
        PolicyFilterChain chain = PolicyFilterChain.Forward,
        IReadOnlyList<ConnectionState>? states = null,
        bool unconstrainedStates = false,
        ServiceSelector? services = null,
        IpsecPolicyPredicate? ipsec = null,
        LogSpecification? logging = null,
        bool enabled = true)
        => PolicyRule.Create(
            family,
            chain,
            PolicyPipelineStage.StatePrelude,
            0,
            TrafficPredicate.Create(
                services: services ?? ServiceSelector.Create([Tcp.Id]),
                connectionStates: unconstrainedStates
                    ? null
                    : states ?? [ConnectionState.Established, ConnectionState.Related],
                ipsecPolicy: ipsec,
                serviceCatalog: Catalog),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept),
            logging ?? LogSpecification.Disabled,
            enabled);

    private static (PolicyRule Rule, Dictionary<ServiceObjectId, ServiceObject> Empty) AnyProtocolRule()
        => (PolicyRule.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                PolicyPipelineStage.StatePrelude,
                0,
                TrafficPredicate.Create(connectionStates: [ConnectionState.Established, ConnectionState.Related]),
                RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept)),
            []);
}
