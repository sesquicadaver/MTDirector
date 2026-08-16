using System.Net;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyEvidenceAnalysisTests
{
    [Fact]
    public void Ac1ManagedOnlyAndNodeEffectiveModesAreSupported()
    {
        PolicyRule allow = AllowRule();
        PolicyTestCase managed = UserTest("managed", PolicyTestExecutionMode.ManagedOnly, allow.Id);
        PolicyTestCase node = UserTest("node", PolicyTestExecutionMode.NodeEffective, allow.Id);
        PolicyEvidenceAnalysisResult managedResult = Analyze([allow], [managed]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.OutcomePass, Assert.Single(managedResult.TestResults).Outcome);
        PolicyEvidenceAnalysisResult missing = Analyze([allow], [node]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.ProofIndeterminate, Assert.Single(missing.TestResults).Proof);
        PolicyEvidenceAnalysisResult withFilter = Analyze(
            [allow],
            [node],
            actualFilter:
            [
                ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", 0, "jump", comment: "fwc:anchor:forward"),
            ]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.OutcomePass, Assert.Single(withFilter.TestResults).Outcome);
        Assert.Equal(allow.Id, Assert.Single(withFilter.TestResults).MatchedRuleId);
    }

    [Fact]
    public void Ac2SystemTestsCannotBeDisabled()
    {
        Assert.Throws<DomainInvariantException>(() =>
            PolicyTestCase.Create(
                "sys",
                PolicyTestOrigin.System,
                PolicyTestExecutionMode.ManagedOnly,
                Packet(),
                PolicyTestExpectedDisposition.Accept,
                enabled: false));
        PolicyTestCase disabled = new()
        {
            Id = PolicyTestId.New(),
            Name = "sys",
            Origin = PolicyTestOrigin.System,
            ExecutionMode = PolicyTestExecutionMode.ManagedOnly,
            Packet = Packet(),
            Expected = PolicyTestExpectedDisposition.Drop,
            Enabled = false,
        };
        PolicyEvidenceAnalysisResult result = Analyze([AllowRule()], [disabled]);
        Assert.Contains(result.Findings, f => f.Code == PolicyEvidenceAnalysisCodes.SystemTestDisabled);
        Assert.True(result.HasBlockers);
        Assert.True(PolicyEvidenceAnalysisCodes.IsFailedPrecondition(PolicyEvidenceAnalysisCodes.SystemTestDisabled));
    }

    [Fact]
    public void Ac3FailedSafetyTestIsBlocker()
    {
        PolicyRule allow = AllowRule();
        PolicyTestCase safety = PolicyTestCase.Create(
            "safety",
            PolicyTestOrigin.System,
            PolicyTestExecutionMode.ManagedOnly,
            Packet(),
            PolicyTestExpectedDisposition.Drop);
        PolicyEvidenceAnalysisResult result = Analyze([allow], [safety]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.OutcomeFail, Assert.Single(result.TestResults).Outcome);
        Assert.Contains(result.Findings, f => f.Code == PolicyEvidenceAnalysisCodes.SafetyTestFailed);
        Assert.True(result.HasBlockers);
        Assert.True(PolicyEvidenceAnalysisCodes.IsFailedPrecondition(PolicyEvidenceAnalysisCodes.SafetyTestFailed));
    }

    [Fact]
    public void Ac4MatchedRuleAndPathAreReturned()
    {
        PolicyRule allow = AllowRule();
        PolicyEvidenceAnalysisResult result = Analyze(
            [allow],
            [UserTest("hit", PolicyTestExecutionMode.ManagedOnly, allow.Id)]);
        PolicyTestResult test = Assert.Single(result.TestResults);
        Assert.Equal(allow.Id, test.MatchedRuleId);
        Assert.Equal(PolicyPipelineStage.CompanyAllow, test.MatchedStage);
        Assert.Contains(test.MatchedPath, h => h.Kind == PolicyTestPathKind.ManagedRule && h.RuleId == allow.Id);
        Assert.Equal(PolicyTestExpectedDisposition.Accept, test.FinalDisposition);
    }

    [Fact]
    public void Ac5ManagedRuleUuidIsUsedForDiff()
    {
        PolicyRule keep = AllowRule();
        PolicyRule added = AllowRule();
        PolicyRevisionDiffResult diff = PolicyRevisionDiffer.Diff(
            [keep],
            [keep, added],
            EmptyAddresses(),
            EmptyAddresses(),
            EmptyServices(),
            EmptyServices(),
            new HashSet<Guid>(),
            new HashSet<Guid>());
        PolicyRuleDiffEntry entry = Assert.Single(diff.RuleChanges);
        Assert.Equal(added.Id, entry.RuleId);
        Assert.Contains(PolicyEvidenceAnalysisCodes.ChangeAdded, entry.Changes);
        Assert.DoesNotContain(diff.RuleChanges, e => e.RuleId == keep.Id);

        PolicyRule reconstituted = PolicyRule.Create(
            keep.Family,
            keep.Chain,
            keep.Stage,
            keep.Ordinal,
            TrafficPredicate.Create(),
            keep.Effect,
            keep.Logging,
            keep.Enabled,
            keep.ExceptionEligible,
            keep.Description,
            keep.Id);
        PolicyRevisionDiffResult identical = PolicyRevisionDiffer.Diff(
            [keep],
            [reconstituted],
            EmptyAddresses(),
            EmptyAddresses(),
            EmptyServices(),
            EmptyServices(),
            new HashSet<Guid>(),
            new HashSet<Guid>());
        Assert.Empty(identical.RuleChanges);
    }

    [Fact]
    public void Ac6AddedRemovedModifiedMovedEnabledDisabledAreDetermined()
    {
        PolicyRule original = AllowRule();
        PolicyRule removed = AllowRule();
        PolicyRule disabled = PolicyRule.Create(
            original.Family,
            original.Chain,
            original.Stage,
            0,
            original.Predicate,
            original.Effect,
            original.Logging,
            enabled: false,
            id: original.Id);
        PolicyRule added = AllowRule();
        PolicyRule modified = PolicyRule.Create(
            original.Family,
            original.Chain,
            original.Stage,
            1,
            original.Predicate,
            original.Effect,
            original.Logging,
            description: "changed",
            id: original.Id);
        PolicyRevisionDiffResult all = PolicyRevisionDiffer.Diff(
            [original, removed],
            [modified, added],
            EmptyAddresses(),
            EmptyAddresses(),
            EmptyServices(),
            EmptyServices(),
            new HashSet<Guid>(),
            new HashSet<Guid>());
        Assert.Contains(
            all.RuleChanges,
            e => e.RuleId == original.Id
                 && e.Changes.Contains(PolicyEvidenceAnalysisCodes.ChangeModified)
                 && e.Changes.Contains(PolicyEvidenceAnalysisCodes.ChangeMoved));
        Assert.Contains(
            all.RuleChanges,
            e => e.RuleId == removed.Id && e.Changes.Contains(PolicyEvidenceAnalysisCodes.ChangeRemoved));
        Assert.Contains(
            all.RuleChanges,
            e => e.RuleId == added.Id && e.Changes.Contains(PolicyEvidenceAnalysisCodes.ChangeAdded));

        PolicyRevisionDiffResult enablement = PolicyRevisionDiffer.Diff(
            [original],
            [disabled],
            EmptyAddresses(),
            EmptyAddresses(),
            EmptyServices(),
            EmptyServices(),
            new HashSet<Guid>(),
            new HashSet<Guid>());
        Assert.Contains(
            enablement.RuleChanges,
            e => e.RuleId == original.Id && e.Changes.Contains(PolicyEvidenceAnalysisCodes.ChangeDisabled));
    }

    [Fact]
    public void Ac7ObjectChangesHaveImpactSet()
    {
        AddressObject before = Host("192.0.2.10");
        AddressObject after = AddressObject.Reconstitute(
            before.Id,
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("expanded"),
            IpAddressFamily.IPv4,
            description: null,
            [AddressInterval.FromPrefix(IpAddressFamily.IPv4, IPAddress.Parse("192.0.2.0"), 24)]);
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([after.Id])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyRevisionDiffResult diff = PolicyRevisionDiffer.Diff(
            [rule],
            [rule],
            new Dictionary<AddressObjectId, AddressObject> { [before.Id] = before },
            new Dictionary<AddressObjectId, AddressObject> { [after.Id] = after },
            EmptyServices(),
            EmptyServices(),
            new HashSet<Guid>(),
            new HashSet<Guid>());
        PolicyObjectImpact impact = Assert.Single(diff.ObjectImpacts);
        Assert.Equal("address", impact.ObjectKind);
        Assert.Equal(before.Id.Value, impact.ObjectId);
        Assert.Contains(rule.Id, impact.DependentRuleIds);
    }

    [Fact]
    public void Ac8NewlyAcceptedAndNewlyDeniedPacketSpacesAreClassified()
    {
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop));
        PolicyRule allow = AllowRule();
        PolicyRevisionDiffResult permissive = PolicyRevisionDiffer.Diff(
            [deny],
            [allow],
            EmptyAddresses(),
            EmptyAddresses(),
            EmptyServices(),
            EmptyServices(),
            new HashSet<Guid>(),
            new HashSet<Guid>());
        Assert.Contains(PolicyEvidenceAnalysisCodes.PacketNewlyAccepted, permissive.PacketSpaceClasses);
        Assert.Contains(PolicyEvidenceAnalysisCodes.ClassPermissive, permissive.SemanticClasses);

        PolicyRevisionDiffResult restrictive = PolicyRevisionDiffer.Diff(
            [allow],
            [deny],
            EmptyAddresses(),
            EmptyAddresses(),
            EmptyServices(),
            EmptyServices(),
            new HashSet<Guid>(),
            new HashSet<Guid>());
        Assert.Contains(PolicyEvidenceAnalysisCodes.PacketNewlyDenied, restrictive.PacketSpaceClasses);
        Assert.Contains(PolicyEvidenceAnalysisCodes.ClassRestrictive, restrictive.SemanticClasses);
    }

    [Fact]
    public void Ac9RiskUsesNormativeMapping()
    {
        PolicyRule allow = AllowRule();
        PolicyEvidenceAnalysisResult addedAllow = Analyze([], [UserTest("t", PolicyTestExecutionMode.ManagedOnly, allow.Id)], after: [allow], before: []);
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskHigh, addedAllow.Risk.Level);
        PolicyEvidenceAnalysisResult comment = Analyze(
            [allow],
            [],
            after: [allow],
            before: [allow]);
        Assert.True(
            comment.Risk.Level is PolicyEvidenceAnalysisCodes.RiskNone or PolicyEvidenceAnalysisCodes.RiskLow);
    }

    [Fact]
    public void Ac10ManagementFastTrackExceptionAndDefaultHaveMinimumRisk()
    {
        PolicyRule ft = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            0,
            TrafficPredicate.Create(
                services: ServiceSelector.Create([Tcp.Id]),
                connectionStates: [ConnectionState.Established, ConnectionState.Related],
                serviceCatalog: Catalog()),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept));
        PolicyEvidenceAnalysisResult fast = Analyze([], [], after: [ft], before: []);
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskHigh, fast.Risk.Level);
        Assert.Contains(PolicyEvidenceAnalysisCodes.ClassFastTrack, fast.Risk.Drivers);

        PolicyEvidenceAnalysisResult management = Analyze(
            [AllowRule()],
            [],
            signals: new PolicyEvidenceSignals { ManagementPathChanged = true });
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskCritical, management.Risk.Level);

        PolicyEvidenceAnalysisResult exception = Analyze(
            [AllowRule()],
            [],
            signals: new PolicyEvidenceSignals { ExceptionChanged = true });
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskHigh, exception.Risk.Level);

        PolicyEvidenceAnalysisResult defaults = Analyze(
            [AllowRule()],
            [],
            signals: new PolicyEvidenceSignals { DefaultDispositionChanged = true });
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskCritical, defaults.Risk.Level);
    }

    [Fact]
    public void Ac11DiffAndRiskAreDeterministic()
    {
        PolicyRule a = AllowRule();
        PolicyRule b = AllowRule();
        PolicyTestCase test = UserTest("t", PolicyTestExecutionMode.ManagedOnly, a.Id);
        PolicyEvidenceAnalysisResult first = Analyze([a], [test], after: [a, b], before: [a]);
        PolicyEvidenceAnalysisResult second = Analyze([a], [test], after: [a, b], before: [a]);
        Assert.Equal(first.EvidenceContextHash.ToString(), second.EvidenceContextHash.ToString());
        Assert.Equal(first.Risk.Level, second.Risk.Level);
        Assert.Equal(
            first.Diff.RuleChanges.Select(e => e.RuleId.Value).OrderBy(g => g),
            second.Diff.RuleChanges.Select(e => e.RuleId.Value).OrderBy(g => g));
    }

    [Fact]
    public void Ac12TestsDiffAndRiskEnterAnalysisContextHash()
    {
        PolicyRule allow = AllowRule();
        PolicyEvidenceAnalysisResult result = Analyze(
            [allow],
            [UserTest("t", PolicyTestExecutionMode.ManagedOnly, allow.Id)]);
        Hash256 actual = ActualFilterAnalysis.HashActualContext([]);
        Hash256 packet = PacketPathAnalysis.HashPacketPathContext([]);
        Hash256 management = ManagementPathAnalysis.HashManagementPathContext(
            ManagementAccessProfile.Create([AddressPrefix.Parse("192.0.2.0/24")], "192.0.2.10", 8729),
            ManagementIpServiceFacts.Create(true, false, "8729", null),
            []);
        Hash256 topology = TopologyDependencyAnalysis.HashTopologyDependencyContext(TopologyDependencyFacts.Create());
        Hash256 fast = FastTrackAnalysis.HashFastTrackContext([], FastTrackTopologyContext.SafeSingleWan);
        Hash256 five = FastTrackAnalysis.HashAnalysisContext(actual, packet, management, topology, fast);
        Hash256 six = PolicyEvidenceAnalysis.HashAnalysisContext(
            actual, packet, management, topology, fast, result.EvidenceContextHash);
        Assert.NotEqual(five.ToString(), six.ToString());
        Assert.Equal(
            five.ToString(),
            FastTrackAnalysis.HashAnalysisContext(actual, packet, management, topology, fast).ToString());
        Assert.Equal(
            six.ToString(),
            PolicyEvidenceAnalysis.HashAnalysisContext(
                actual, packet, management, topology, fast, result.EvidenceContextHash).ToString());
        Assert.False(PolicyEvidenceAnalysisCodes.IsFailedPrecondition(string.Empty));
        Assert.True(PolicyEvidenceAnalysisCodes.IsFailedPrecondition(
            PolicyEvidenceAnalysisCodes.NodeEffectiveIndeterminate));
    }

    [Fact]
    public void DisabledUserTestsPassAndInvariantsRejectEmptyInputs()
    {
        Assert.Throws<DomainInvariantException>(() =>
            PolicyTestPacket.Create(IpAddressFamily.IPv4, PolicyFilterChain.Forward, " ", "192.0.2.2"));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyTestCase.Create(
                " ",
                PolicyTestOrigin.User,
                PolicyTestExecutionMode.ManagedOnly,
                Packet(),
                PolicyTestExpectedDisposition.Accept));
        PolicyTestCase disabled = PolicyTestCase.Create(
            "user-off",
            PolicyTestOrigin.User,
            PolicyTestExecutionMode.ManagedOnly,
            Packet(),
            PolicyTestExpectedDisposition.Accept,
            enabled: false);
        PolicyEvidenceAnalysisResult result = Analyze([AllowRule()], [disabled]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.OutcomePass, Assert.Single(result.TestResults).Outcome);
        Assert.False(result.HasBlockers);
    }

    [Fact]
    public void ZoneServiceRejectDenyAndBindingFloorsAreCovered()
    {
        Guid zoneId = Guid.NewGuid();
        ServiceObject udp = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("udp"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Udp, "udp"))]);
        ServiceObject udpChanged = ServiceObject.Reconstitute(
            udp.Id,
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("udp-changed"),
            description: null,
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Udp, "udp"))]);
        PolicyRule serviceRule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(services: ServiceSelector.Create([udp.Id])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyRevisionDiffResult zones = PolicyRevisionDiffer.Diff(
            [AllowRule()],
            [AllowRule()],
            EmptyAddresses(),
            EmptyAddresses(),
            EmptyServices(),
            EmptyServices(),
            new HashSet<Guid>(),
            new HashSet<Guid> { zoneId });
        Assert.Contains(zones.ObjectImpacts, i => i.ObjectKind == "zone" && i.ObjectId == zoneId);

        PolicyRevisionDiffResult services = PolicyRevisionDiffer.Diff(
            [serviceRule],
            [serviceRule],
            EmptyAddresses(),
            EmptyAddresses(),
            new Dictionary<ServiceObjectId, ServiceObject> { [udp.Id] = udp },
            new Dictionary<ServiceObjectId, ServiceObject> { [udp.Id] = udpChanged },
            new HashSet<Guid>(),
            new HashSet<Guid>());
        Assert.Contains(services.ObjectImpacts, i => i.ObjectKind == "service" && i.ObjectId == udp.Id.Value);

        AddressObject before = Host("192.0.2.10");
        AddressObject after = AddressObject.Reconstitute(
            before.Id,
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("expanded"),
            IpAddressFamily.IPv4,
            description: null,
            [AddressInterval.FromPrefix(IpAddressFamily.IPv4, IPAddress.Parse("192.0.2.0"), 24)]);
        PolicyRule addrRule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([after.Id])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyEvidenceAnalysisResult hashed = PolicyEvidenceAnalysis.Analyze(
            [addrRule],
            [],
            Contracts(),
            new Dictionary<AddressObjectId, AddressObject> { [after.Id] = after },
            EmptyServices(),
            [addrRule],
            new Dictionary<AddressObjectId, AddressObject> { [before.Id] = before },
            EmptyServices());
        Assert.Contains(hashed.Diff.ObjectImpacts, i => i.ObjectKind == "address");
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskHigh, hashed.Risk.Level);

        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop));
        PolicyRule reject = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.AdminProhibited));
        PolicyRule allow = AllowRule();
        Assert.Equal(
            PolicyEvidenceAnalysisCodes.RiskMedium,
            Analyze([], [], after: [deny], before: []).Risk.Level);
        Assert.Equal(
            PolicyEvidenceAnalysisCodes.RiskMedium,
            Analyze([allow], [], after: [], before: [allow]).Risk.Level);
        PolicyRule disabled = PolicyRule.Create(
            allow.Family,
            allow.Chain,
            allow.Stage,
            allow.Ordinal,
            allow.Predicate,
            allow.Effect,
            allow.Logging,
            enabled: false,
            id: allow.Id);
        PolicyRiskResult disabledRisk = PolicyRiskClassifier.Classify(
            new PolicyRevisionDiffResult
            {
                RuleChanges =
                [
                    new PolicyRuleDiffEntry
                    {
                        RuleId = allow.Id,
                        Changes = [PolicyEvidenceAnalysisCodes.ChangeDisabled],
                    },
                ],
                ObjectImpacts = [],
                PacketSpaceClasses = [],
                SemanticClasses = [PolicyEvidenceAnalysisCodes.ClassNoEffectiveChange],
            },
            [],
            PolicyEvidenceSignals.None,
            [allow],
            [disabled]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskLow, disabledRisk.Level);
        Assert.Contains(
            PolicyEvidenceAnalysisCodes.PacketRejectChanged,
            PolicyRevisionDiffer.Diff(
                [deny],
                [reject],
                EmptyAddresses(),
                EmptyAddresses(),
                EmptyServices(),
                EmptyServices(),
                new HashSet<Guid>(),
                new HashSet<Guid>()).PacketSpaceClasses);

        PolicyEvidenceAnalysisResult binding = Analyze(
            [allow],
            [],
            signals: new PolicyEvidenceSignals { ZoneBindingChanged = true });
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskCritical, binding.Risk.Level);

        PolicyRevisionDiffResult semantic = new()
        {
            RuleChanges = [],
            ObjectImpacts = [],
            PacketSpaceClasses = [],
            SemanticClasses =
            [
                PolicyEvidenceAnalysisCodes.ClassRestrictive,
                PolicyEvidenceAnalysisCodes.ClassFastTrack,
                PolicyEvidenceAnalysisCodes.ClassException,
                PolicyEvidenceAnalysisCodes.ClassControlPlane,
                PolicyEvidenceAnalysisCodes.ClassDefaultDisposition,
                PolicyEvidenceAnalysisCodes.ClassZoneBinding,
                "UNKNOWN_SEMANTIC",
            ],
        };
        PolicyRiskResult classified = PolicyRiskClassifier.Classify(
            semantic,
            [],
            PolicyEvidenceSignals.None,
            [],
            []);
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskCritical, classified.Level);
    }

    [Fact]
    public void NodeEffectiveUnknownMatcherAndPostAnchorAreProven()
    {
        PolicyRule allow = AllowRule();
        PolicyTestCase node = UserTest("node", PolicyTestExecutionMode.NodeEffective, allow.Id);
        PolicyEvidenceAnalysisResult unknown = Analyze(
            [allow],
            [node],
            actualFilter:
            [
                ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "forward",
                    0,
                    "accept",
                    comment: "unmanaged",
                    unknownMatchers: new Dictionary<string, string>(StringComparer.Ordinal) { ["nth"] = "1,1" }),
                ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", 1, "jump", comment: "fwc:anchor:forward"),
            ]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.ProofIndeterminate, Assert.Single(unknown.TestResults).Proof);

        PolicyTestCase post = PolicyTestCase.Create(
            "post",
            PolicyTestOrigin.User,
            PolicyTestExecutionMode.NodeEffective,
            Packet(),
            PolicyTestExpectedDisposition.Accept);
        PolicyEvidenceAnalysisResult postHit = Analyze(
            [],
            [post],
            actualFilter:
            [
                ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", 0, "jump", comment: "fwc:anchor:forward"),
                ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", 1, "accept", comment: "unmanaged"),
            ]);
        PolicyTestResult result = Assert.Single(postHit.TestResults);
        Assert.Equal(PolicyEvidenceAnalysisCodes.OutcomePass, result.Outcome);
        Assert.Contains(result.MatchedPath, h => h.Kind == PolicyTestPathKind.PostAnchorRule);
    }

    [Fact]
    public void NodeEffectiveUnevaluatedActualMatchersAreIndeterminate()
    {
        PolicyRule allow = AllowRule();
        PolicyTestCase node = UserTest("node", PolicyTestExecutionMode.NodeEffective, allow.Id);
        PolicyEvidenceAnalysisResult cidr = Analyze(
            [allow],
            [node],
            actualFilter:
            [
                ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "forward",
                    0,
                    "drop",
                    comment: "unmanaged",
                    knownMatchers: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["src-address"] = "10.0.0.0/8",
                    }),
                ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", 1, "jump", comment: "fwc:anchor:forward"),
            ]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.ProofIndeterminate, Assert.Single(cidr.TestResults).Proof);
        Assert.NotEqual(PolicyEvidenceAnalysisCodes.OutcomePass, Assert.Single(cidr.TestResults).Outcome);

        PolicyEvidenceAnalysisResult extra = Analyze(
            [allow],
            [node],
            actualFilter:
            [
                ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "forward",
                    0,
                    "drop",
                    comment: "unmanaged",
                    knownMatchers: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["src-port"] = "12345",
                    }),
                ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", 1, "jump", comment: "fwc:anchor:forward"),
            ]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.ProofIndeterminate, Assert.Single(extra.TestResults).Proof);

        PolicyEvidenceAnalysisResult jump = Analyze(
            [allow],
            [node],
            actualFilter:
            [
                ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", 0, "jump", comment: "unmanaged"),
                ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", 1, "jump", comment: "fwc:anchor:forward"),
            ]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.ProofIndeterminate, Assert.Single(jump.TestResults).Proof);

        PolicyEvidenceAnalysisResult ospf = Analyze(
            [allow],
            [node],
            actualFilter:
            [
                ActualFilterRule.Create(
                    IpAddressFamily.IPv4,
                    "forward",
                    0,
                    "drop",
                    comment: "unmanaged",
                    knownMatchers: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["protocol"] = "ospf",
                    }),
                ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", 1, "jump", comment: "fwc:anchor:forward"),
            ]);
        Assert.Equal(PolicyEvidenceAnalysisCodes.ProofIndeterminate, Assert.Single(ospf.TestResults).Proof);
    }

    [Fact]
    public void IcmpConstrainedRuleWithoutTypeIsIndeterminate()
    {
        ServiceObject echo = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("echo"),
            [
                ServiceTerm.Create(
                    IpProtocol.Create(IpProtocol.Icmp, "icmp"),
                    icmpSelectors: IcmpSelectorSet.Create([new IcmpSelector(8)])),
            ]);
        PolicyRule icmpAllow = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(
                services: ServiceSelector.Create([echo.Id]),
                serviceCatalog: new Dictionary<ServiceObjectId, ServiceObject> { [echo.Id] = echo }),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        PolicyTestCase test = PolicyTestCase.Create(
            "icmp",
            PolicyTestOrigin.User,
            PolicyTestExecutionMode.ManagedOnly,
            PolicyTestPacket.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                "192.0.2.1",
                "192.0.2.2",
                protocol: IpProtocol.Icmp),
            PolicyTestExpectedDisposition.Accept,
            icmpAllow.Id);
        PolicyEvidenceAnalysisResult result = PolicyEvidenceAnalysis.Analyze(
            [icmpAllow],
            [test],
            Contracts(),
            EmptyAddresses(),
            new Dictionary<ServiceObjectId, ServiceObject> { [echo.Id] = echo });
        Assert.Equal(PolicyEvidenceAnalysisCodes.ProofIndeterminate, Assert.Single(result.TestResults).Proof);
    }

    private static readonly ServiceObject Tcp = ServiceObject.Create(
        PolicyObjectOwnerScope.Company,
        null,
        null,
        NonEmptyName.Create("tcp"),
        [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "tcp"))]);

    private static Dictionary<ServiceObjectId, ServiceObject> Catalog()
        => new() { [Tcp.Id] = Tcp };

    private static Dictionary<AddressObjectId, AddressObject> EmptyAddresses() => [];

    private static Dictionary<ServiceObjectId, ServiceObject> EmptyServices() => [];

    private static ChainContractSet Contracts()
        => ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.Drop,
                    rejectMode: null,
                    PolicyRuntimeMode.ManagedOnly),
            ],
            PolicyRuntimeMode.ManagedOnly);

    private static PolicyRule AllowRule()
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));

    private static PolicyTestPacket Packet()
        => PolicyTestPacket.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            "192.0.2.1",
            "192.0.2.2",
            protocol: IpProtocol.Tcp,
            sourcePort: 12345,
            destinationPort: 443,
            connectionState: ConnectionState.New);

    private static PolicyTestCase UserTest(string name, PolicyTestExecutionMode mode, RuleId expectedRule)
        => PolicyTestCase.Create(
            name,
            PolicyTestOrigin.User,
            mode,
            Packet(),
            PolicyTestExpectedDisposition.Accept,
            expectedRule);

    private static AddressObject Host(string ip)
        => AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("host"),
            IpAddressFamily.IPv4,
            [AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse(ip))]);

    private static PolicyEvidenceAnalysisResult Analyze(
        IReadOnlyList<PolicyRule> rules,
        IReadOnlyList<PolicyTestCase> tests,
        IReadOnlyList<PolicyRule>? after = null,
        IReadOnlyList<PolicyRule>? before = null,
        IReadOnlyList<ActualFilterRule>? actualFilter = null,
        PolicyEvidenceSignals? signals = null)
        => PolicyEvidenceAnalysis.Analyze(
            after ?? rules,
            tests,
            Contracts(),
            EmptyAddresses(),
            EmptyServices(),
            before ?? rules,
            actualFilter: actualFilter,
            signals: signals);
}
