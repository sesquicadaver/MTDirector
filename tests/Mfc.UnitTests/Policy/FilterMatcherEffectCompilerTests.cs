using System.Net;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class FilterMatcherEffectCompilerTests
{
    private static readonly DeviceId Device = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    private static readonly Guid RuleGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Ac1NormativeMatchersHaveExactMapping()
    {
        ZoneId lan = ZoneId.New();
        AddressObject src = CompanyAddress("src", AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        AddressObject dst = CompanyAddress("dst", AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.2")));
        ServiceObject http = TcpService("http", 443);
        Dictionary<ServiceObjectId, ServiceObject> services = new() { [http.Id] = http };
        Dictionary<AddressObjectId, AddressObject> addresses = new()
        {
            [src.Id] = src,
            [dst.Id] = dst,
        };
        FilterMatcherCompileContext context = Context(
            Binding(lan, NodeZoneBindingKind.InterfaceList, ["LAN"], ["ether1"]),
            Observation(lists: [List("LAN")], members: [Member("LAN", "ether1")]),
            services,
            addresses);

        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 3,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([src.Id]),
                destinationAddresses: AddressSelector.Create([dst.Id]),
                ingressZones: ZoneSelector.Create([lan]),
                services: ServiceSelector.Create([http.Id]),
                connectionStates: [ConnectionState.Established, ConnectionState.New],
                connectionNatStates: [ConnectionNatState.DstNat, ConnectionNatState.SrcNat],
                sourceAddressTypes: [AddressType.Unicast, AddressType.Local],
                destinationAddressTypes: [AddressType.Unicast],
                tcpFlags: TcpFlagConstraint.Create(
                    requiredPresent: [TcpHeaderBit.Syn],
                    requiredAbsent: [TcpHeaderBit.Ack]),
                ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.In, IpsecPolicyKind.Ipsec),
                serviceCatalog: services),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            logging: LogSpecification.Create(true, "mfc"),
            id: new RuleId(RuleGuid));

        FilterRuleCompileResult result = new FilterMatcherEffectCompiler().Compile([rule], context);
        Assert.True(result.IsSuccess);
        FilterRuleArtifact artifact = Assert.Single(result.Rules);
        Assert.Equal(2, result.InternedLists.Count);
        Assert.Equal("src-address-list", KeyFor(artifact, "src-address-list").Key);
        Assert.Equal(result.InternedLists.Single(l => l.Entries.Any(e => e.Address == "10.0.0.1")).Name, artifact.Matchers["src-address-list"]);
        Assert.Equal("dst-address-list", KeyFor(artifact, "dst-address-list").Key);
        Assert.Equal("in-interface-list", KeyFor(artifact, "in-interface-list").Key);
        Assert.Equal("LAN", artifact.Matchers["in-interface-list"]);
        Assert.Equal("6", artifact.Matchers["protocol"]);
        Assert.Equal("443", artifact.Matchers["dst-port"]);
        Assert.Equal("new,established", artifact.Matchers["connection-state"]);
        Assert.Equal("srcnat,dstnat", artifact.Matchers["connection-nat-state"]);
        Assert.Equal("local,unicast", artifact.Matchers["src-address-type"]);
        Assert.Equal("unicast", artifact.Matchers["dst-address-type"]);
        Assert.Equal("syn,!ack", artifact.Matchers["tcp-flags"]);
        Assert.Equal("in,ipsec", artifact.Matchers["ipsec-policy"]);
        Assert.DoesNotContain(artifact.Matchers.Keys, k => k is "src-address" or "dst-address" or "packet-mark");
        Assert.True(artifact.Log);
        Assert.Equal("mfc", artifact.LogPrefix);
    }

    [Fact]
    public void Ac2UnsupportedTokenIsCompileError()
    {
        Assert.False(RouterOsCompilerProfile.IsSupportedMatcherKey("packet-mark"));
        Assert.False(RouterOsCompilerProfile.TryNormalizeMatcher(
            "packet-mark",
            "x",
            out _,
            out _,
            out string? keyCode));
        Assert.Equal(PolicyCompilerCodes.UnsupportedMatcher, keyCode);
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(PolicyCompilerCodes.UnsupportedMatcher));

        Assert.False(RouterOsCompilerProfile.TryFormatRejectWith((RejectMode)99, out _, out string? rejectCode));
        Assert.Equal(PolicyCompilerCodes.RejectModeUnsupported, rejectCode);

        Assert.False(RouterOsCompilerProfile.TryNormalizeProtocol(
            "not-a-router-os-token",
            out _,
            out string? protocolCode));
        Assert.Equal(PolicyCompilerCodes.UnsupportedMatcher, protocolCode);
        Assert.False(RouterOsCompilerProfile.TryNormalizeMatcher(
            "connection-state",
            "   ",
            out _,
            out _,
            out string? blankCode));
        Assert.Equal(PolicyCompilerCodes.UnsupportedMatcher, blankCode);

        // Number is authoritative; mismatched CanonicalName must not rematch by display label.
        ServiceObject mislabeled = Service(
            "mislabeled-tcp",
            ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "udp")));
        FilterRuleCompileResult numericWins = Compile(
            AcceptRule(
                TrafficPredicate.Create(
                    services: ServiceSelector.Create([mislabeled.Id]),
                    serviceCatalog: new Dictionary<ServiceObjectId, ServiceObject> { [mislabeled.Id] = mislabeled })),
            services: new Dictionary<ServiceObjectId, ServiceObject> { [mislabeled.Id] = mislabeled });
        Assert.True(numericWins.IsSuccess);
        Assert.Equal("6", Assert.Single(numericWins.Rules).Matchers["protocol"]);
        Assert.NotEqual("17", Assert.Single(numericWins.Rules).Matchers["protocol"]);
    }

    [Fact]
    public void Ac3AcceptDropRejectCompileExactly()
    {
        FilterRuleCompileResult accept = Compile(AcceptRule());
        Assert.True(accept.IsSuccess);
        Assert.Equal("accept", Assert.Single(accept.Rules).Action);
        Assert.Empty(Assert.Single(accept.Rules).ActionParameters);

        FilterRuleCompileResult drop = Compile(DenyRule(RuleEffectSpec.Create(PolicyRuleEffect.Drop)));
        Assert.True(drop.IsSuccess);
        Assert.Equal("drop", Assert.Single(drop.Rules).Action);
        Assert.Empty(Assert.Single(drop.Rules).ActionParameters);

        FilterRuleCompileResult reject = Compile(
            DenyRule(RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.AdminProhibited)));
        Assert.True(reject.IsSuccess);
        FilterRuleArtifact artifact = Assert.Single(reject.Rules);
        Assert.Equal("reject", artifact.Action);
        Assert.Equal("icmp-admin-prohibited", artifact.ActionParameters["reject-with"]);
        Assert.NotEqual("drop", artifact.Action);
    }

    [Fact]
    public void Ac4RejectIsNeverReplacedWithDrop()
    {
        ServiceObject tcp = TcpService("tcp", 22);
        Dictionary<ServiceObjectId, ServiceObject> services = new() { [tcp.Id] = tcp };
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            ordinal: 0,
            TrafficPredicate.Create(
                services: ServiceSelector.Create([tcp.Id]),
                serviceCatalog: services),
            RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.TcpReset));

        FilterRuleCompileResult result = Compile(rule, services: services);
        Assert.True(result.IsSuccess);
        FilterRuleArtifact artifact = Assert.Single(result.Rules);
        Assert.Equal("reject", artifact.Action);
        Assert.Equal("tcp-reset", artifact.ActionParameters["reject-with"]);
        Assert.DoesNotContain(artifact.ActionParameters.Keys, static k => k == "drop");
        Assert.NotEqual("drop", artifact.Action);
        Assert.Equal("6", artifact.Matchers["protocol"]);
    }

    [Fact]
    public void Ac5ExceptionCompilesAsReturn()
    {
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage),
            id: new RuleId(RuleGuid));

        FilterRuleCompileResult result = Compile(rule);
        Assert.True(result.IsSuccess);
        FilterRuleArtifact artifact = Assert.Single(result.Rules);
        Assert.Equal("return", artifact.Action);
        Assert.Empty(artifact.ActionParameters);
        Assert.Equal(CompilerComments.Exception(RuleGuid, 0), artifact.Comment);
        Assert.EndsWith(":ex", artifact.Comment, StringComparison.Ordinal);
        Assert.Equal(RuleGuid, artifact.LogicalRuleId);
    }

    [Fact]
    public void Ac6StructuralJumpsHaveDeterministicComments()
    {
        Assert.Equal(CompilerComments.JumpCompanyDeny, ManagedChainLayoutBuilder.JumpCompanyDenyComment);
        Assert.Equal(CompilerComments.JumpSiteDeny, ManagedChainLayoutBuilder.JumpSiteDenyComment);
        Assert.Equal(CompilerComments.JumpNodeDeny, ManagedChainLayoutBuilder.JumpNodeDenyComment);
        Assert.Equal(CompilerComments.ReturnCompanyDeny, ManagedChainLayoutBuilder.ReturnCompanyDenyComment);
        Assert.Equal(CompilerComments.ReturnSiteDeny, ManagedChainLayoutBuilder.ReturnSiteDenyComment);
        Assert.Equal(CompilerComments.ReturnNodeDeny, ManagedChainLayoutBuilder.ReturnNodeDenyComment);
        Assert.Equal(CompilerComments.Terminal, ManagedChainLayoutBuilder.TerminalComment);
        Assert.Equal("mfc:s:jump:company-deny", CompilerComments.JumpCompanyDeny);
        Assert.Equal("mfc:s:jump:site-deny", CompilerComments.JumpSiteDeny);
        Assert.Equal("mfc:s:jump:node-deny", CompilerComments.JumpNodeDeny);
        Assert.Equal("mfc:s:return:company-deny", CompilerComments.ReturnCompanyDeny);
        Assert.Equal("mfc:s:return:site-deny", CompilerComments.ReturnSiteDeny);
        Assert.Equal("mfc:s:return:node-deny", CompilerComments.ReturnNodeDeny);
        Assert.Equal("mfc:s:terminal", CompilerComments.Terminal);
        Assert.Equal("tcp-reset", RouterOsCompilerProfile.FormatRejectWith(RejectMode.TcpReset));
        Assert.Equal("icmp-admin-prohibited", RouterOsCompilerProfile.FormatRejectWith(RejectMode.AdminProhibited));
        Assert.Equal("icmp-port-unreachable", RouterOsCompilerProfile.FormatRejectWith(RejectMode.PortUnreachable));
    }

    [Fact]
    public void Ac7LogicalRuleVariantsAreAdjacent()
    {
        ZoneId lan = ZoneId.New();
        FilterMatcherCompileContext context = Context(
            Binding(lan, NodeZoneBindingKind.ExplicitInterfaceSet, ["ether2", "ether1"], ["ether1", "ether2"]),
            Observation());
        PolicyRule expanded = AcceptRule(
            TrafficPredicate.Create(ingressZones: ZoneSelector.Create([lan])),
            ordinal: 0,
            id: new RuleId(RuleGuid));
        PolicyRule next = AcceptRule(ordinal: 1, id: new RuleId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));

        FilterRuleCompileResult result = new FilterMatcherEffectCompiler().Compile([expanded, next], context);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Rules.Count);
        Assert.Equal(RuleGuid, result.Rules[0].LogicalRuleId);
        Assert.Equal(0u, result.Rules[0].VariantIndex);
        Assert.Equal("ether1", result.Rules[0].Matchers["in-interface"]);
        Assert.Equal(RuleGuid, result.Rules[1].LogicalRuleId);
        Assert.Equal(1u, result.Rules[1].VariantIndex);
        Assert.Equal("ether2", result.Rules[1].Matchers["in-interface"]);
        Assert.Equal(next.Id.Value, result.Rules[2].LogicalRuleId);
        Assert.Equal(0u, result.Rules[2].VariantIndex);
        Assert.Equal(0u, result.Rules[0].Ordinal);
        Assert.Equal(1u, result.Rules[1].Ordinal);
        Assert.Equal(2u, result.Rules[2].Ordinal);
    }

    [Fact]
    public void Ac8CompilerDoesNotReorderLogicalRules()
    {
        PolicyRule first = AcceptRule(ordinal: 9, id: new RuleId(Guid.Parse("99999999-9999-9999-9999-999999999999")));
        PolicyRule second = AcceptRule(ordinal: 1, id: new RuleId(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        FilterRuleCompileResult result = CompileAll([first, second]);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Rules.Count);
        Assert.Equal(first.Id.Value, result.Rules[0].LogicalRuleId);
        Assert.Equal(second.Id.Value, result.Rules[1].LogicalRuleId);
        Assert.Equal(CompilerComments.LogicalRule(first.Id.Value, 0), result.Rules[0].Comment);
        Assert.Equal(CompilerComments.LogicalRule(second.Id.Value, 0), result.Rules[1].Comment);
    }

    [Fact]
    public void Ac9CompilerDoesNotDeleteDuplicates()
    {
        PolicyRule a = AcceptRule(ordinal: 0);
        PolicyRule b = AcceptRule(ordinal: 1);
        FilterRuleCompileResult result = CompileAll([a, b]);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Rules.Count);
        Assert.All(result.Rules, r => Assert.Equal("accept", r.Action));
        Assert.NotEqual(result.Rules[0].LogicalRuleId, result.Rules[1].LogicalRuleId);
        Assert.Equal(a.Id.Value, result.Rules[0].LogicalRuleId);
        Assert.Equal(b.Id.Value, result.Rules[1].LogicalRuleId);
    }

    [Fact]
    public void Ac10GeneratedCommentsContainNoUserMetadata()
    {
        const string description = "ticket SECRET-99 user alice site HQ 1.2.3.4";
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            description: description,
            id: new RuleId(RuleGuid));

        FilterRuleCompileResult result = Compile(rule);
        Assert.True(result.IsSuccess);
        FilterRuleArtifact artifact = Assert.Single(result.Rules);
        Assert.Equal($"mfc:r:{RuleGuid:D}:0", artifact.Comment);
        Assert.DoesNotContain(description, artifact.Comment, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET", artifact.Comment, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", artifact.Comment, StringComparison.Ordinal);
        Assert.DoesNotContain("HQ", artifact.Comment, StringComparison.Ordinal);
        Assert.DoesNotContain("1.2.3.4", artifact.Comment, StringComparison.Ordinal);
        Assert.DoesNotContain("ticket", artifact.Comment, StringComparison.Ordinal);
        Assert.True(artifact.Comment.Length <= CompilerComments.LayoutV1MaxAsciiBytes);
        Assert.StartsWith("mfc:r:", artifact.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void FastTrackFailsClosedWithoutEmittingAPair()
    {
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept));
        FilterRuleCompileResult result = Compile(rule);
        Assert.False(result.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.FasttrackContextUnsupported, result.Code);
        Assert.Empty(result.Rules);
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(result.Code!));
    }

    [Fact]
    public void DisabledRulesAreOmittedAndFailureDropsPartialLists()
    {
        AddressObject host = CompanyAddress("h", AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.8")));
        PolicyRule disabled = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            enabled: false);
        PolicyRule listed = AcceptRule(
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([host.Id])),
            ordinal: 1);
        ZoneId missing = ZoneId.New();
        PolicyRule unresolved = AcceptRule(
            TrafficPredicate.Create(ingressZones: ZoneSelector.Create([missing])),
            ordinal: 2);

        FilterRuleCompileResult result = new FilterMatcherEffectCompiler().Compile(
            [disabled, listed, unresolved],
            Context(addresses: new Dictionary<AddressObjectId, AddressObject> { [host.Id] = host }));
        Assert.False(result.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.ZoneNotResolved, result.Code);
        Assert.Empty(result.Rules);
        Assert.Empty(result.InternedLists);
    }

    [Fact]
    public void FilterRuleLimitIsEnforcedPerFamilyChain()
    {
        FilterMatcherEffectCompiler compiler = new(new FilterRuleCompileLimits { MaxPhysicalRulesPerFamilyChain = 1 });
        FilterRuleCompileResult fail = compiler.Compile([AcceptRule(), AcceptRule()], Context());
        Assert.False(fail.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.FilterRuleLimit, fail.Code);
        Assert.Empty(fail.Rules);
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(fail.Code!));

        Assert.Throws<DomainInvariantException>(() =>
            new FilterMatcherEffectCompiler(new FilterRuleCompileLimits { MaxPhysicalRulesPerFamilyChain = 0 }));
        Assert.Throws<DomainInvariantException>(() =>
            new FilterMatcherEffectCompiler(new FilterRuleCompileLimits
            {
                MaxPhysicalRulesPerFamilyChain = FilterRuleCompileLimits.LayoutV1MaxPhysicalRulesPerFamilyChain + 1,
            }));
    }

    [Fact]
    public void Ipv6IcmpUsesIcmpOptionsAndNumericProtocol()
    {
        ServiceObject icmp6 = Service(
            "icmp6",
            ServiceTerm.Create(
                IpProtocol.Create(IpProtocol.IcmpV6, "icmpv6"),
                icmpSelectors: IcmpSelectorSet.Create([new IcmpSelector(128)])));
        FilterRuleCompileResult result = Compile(
            PolicyRule.Create(
                IpAddressFamily.IPv6,
                PolicyFilterChain.Forward,
                PolicyPipelineStage.CompanyAllow,
                ordinal: 0,
                TrafficPredicate.Create(
                    services: ServiceSelector.Create([icmp6.Id]),
                    serviceCatalog: new Dictionary<ServiceObjectId, ServiceObject> { [icmp6.Id] = icmp6 }),
                RuleEffectSpec.Create(PolicyRuleEffect.Accept)),
            services: new Dictionary<ServiceObjectId, ServiceObject> { [icmp6.Id] = icmp6 });
        Assert.True(result.IsSuccess);
        FilterRuleArtifact artifact = Assert.Single(result.Rules);
        Assert.Equal("58", artifact.Matchers["protocol"]);
        Assert.Equal("128", artifact.Matchers["icmp-options"]);
    }

    [Fact]
    public void EmptyInputSucceedsWithoutRules()
    {
        FilterRuleCompileResult result = CompileAll([]);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Rules);
        Assert.Empty(result.InternedLists);
    }

    private static KeyValuePair<string, string> KeyFor(FilterRuleArtifact artifact, string key)
        => artifact.Matchers.Single(m => m.Key == key);

    private static FilterRuleCompileResult Compile(
        PolicyRule rule,
        Dictionary<ServiceObjectId, ServiceObject>? services = null,
        Dictionary<AddressObjectId, AddressObject>? addresses = null)
        => new FilterMatcherEffectCompiler().Compile([rule], Context(catalog: services, addresses: addresses));

    private static FilterRuleCompileResult CompileAll(IReadOnlyList<PolicyRule> rules)
        => new FilterMatcherEffectCompiler().Compile(rules, Context());

    private static PolicyRule AcceptRule(
        TrafficPredicate? predicate = null,
        uint ordinal = 0,
        RuleId? id = null)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal,
            predicate ?? TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: id);

    private static PolicyRule DenyRule(RuleEffectSpec effect)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            ordinal: 0,
            TrafficPredicate.Create(),
            effect);

    private static FilterMatcherCompileContext Context(
        params NodeZoneBinding[] bindings)
        => Context(bindings, Observation(), catalog: null, addresses: null);

    private static FilterMatcherCompileContext Context(
        NodeZoneBinding binding,
        ZoneResolveDeviceObservation observation,
        Dictionary<ServiceObjectId, ServiceObject>? catalog = null,
        Dictionary<AddressObjectId, AddressObject>? addresses = null)
        => Context([binding], observation, catalog, addresses);

    private static FilterMatcherCompileContext Context(
        IReadOnlyList<NodeZoneBinding>? bindings = null,
        ZoneResolveDeviceObservation? observation = null,
        Dictionary<ServiceObjectId, ServiceObject>? catalog = null,
        Dictionary<AddressObjectId, AddressObject>? addresses = null)
        => new()
        {
            Zones = new ZoneServiceCompileContext
            {
                DeviceId = Device,
                Bindings = (bindings ?? []).ToDictionary(static b => b.ZoneId),
                Observation = observation ?? Observation(),
                Services = catalog ?? new Dictionary<ServiceObjectId, ServiceObject>(),
            },
            Addresses = addresses ?? new Dictionary<AddressObjectId, AddressObject>(),
        };

    private static NodeZoneBinding Binding(
        ZoneId zoneId,
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        IReadOnlyList<string> resolvedMembers)
    {
        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(kind, values, resolvedMembers);
        return NodeZoneBinding.Create(new NodeId(Guid.NewGuid()), zoneId, kind, values, expected);
    }

    private static ZoneResolveDeviceObservation Observation(
        IReadOnlyList<InterfaceListSpec>? lists = null,
        IReadOnlyList<InterfaceListMemberSpec>? members = null)
        => new()
        {
            DeviceId = Device,
            ObservationAvailable = true,
            Interfaces =
            [
                new ZoneResolveInterfaceObservation { Name = "ether1", Dynamic = false },
                new ZoneResolveInterfaceObservation { Name = "ether2", Dynamic = false },
            ],
            InterfaceLists = lists ?? [],
            InterfaceListMembers = members ?? [],
        };

    private static InterfaceListSpec List(string name)
        => new()
        {
            Name = name,
            Include = [],
            Exclude = [],
        };

    private static InterfaceListMemberSpec Member(string list, string iface)
        => new()
        {
            List = list,
            Interface = iface,
            Disabled = false,
        };

    private static AddressObject CompanyAddress(string name, params AddressEntry[] entries)
        => AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create(name),
            IpAddressFamily.IPv4,
            entries);

    private static ServiceObject TcpService(string name, ushort port)
        => Service(
            name,
            ServiceTerm.Create(
                IpProtocol.Create(IpProtocol.Tcp, "tcp"),
                destinationPorts: PortSet.Create([new PortInterval(port, port)])));

    private static ServiceObject Service(string name, params ServiceTerm[] terms)
        => ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create(name),
            terms);
}
