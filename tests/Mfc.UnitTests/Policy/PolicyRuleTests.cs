using System.Reflection;
using System.Text;
using System.Text.Json;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyRuleTests
{
    [Fact]
    public void D1EffectsAndRejectModeValidation()
    {
        PolicyRule accept = ValidRule(
            PolicyPipelineStage.CompanyAllow,
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
        Assert.Equal(PolicyRuleEffect.Accept, accept.Effect.Kind);

        PolicyRule drop = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Drop));
        Assert.Equal(PolicyRuleEffect.Drop, drop.Effect.Kind);

        PolicyRule reject = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.AdminProhibited));
        Assert.Equal(RejectMode.AdminProhibited, reject.Effect.RejectModeValue);

        PolicyRule fasttrack = ValidRule(
            PolicyPipelineStage.StatePrelude,
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept),
            chain: PolicyFilterChain.Forward);
        Assert.Equal(PolicyRuleEffect.FasttrackAccept, fasttrack.Effect.Kind);

        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage));
        Assert.Equal(PolicyRuleEffect.ExemptDenyStage, exempt.Effect.Kind);

        Assert.Throws<DomainInvariantException>(() =>
            RuleEffectSpec.Create(PolicyRuleEffect.Reject));
        Assert.Throws<DomainInvariantException>(() =>
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept, RejectMode.TcpReset));
        Assert.Throws<DomainInvariantException>(() =>
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage, RejectMode.AdminProhibited));
        Assert.Throws<DomainInvariantException>(() =>
            RuleEffectSpec.Create(PolicyRuleEffect.Accept, RejectMode.PortUnreachable));
        Assert.Throws<DomainInvariantException>(() =>
            ValidRule(
                PolicyPipelineStage.CompanyAllow,
                RuleEffectSpec.Create(PolicyRuleEffect.Drop)));
    }

    [Fact]
    public void D2TcpResetRequiresTcpOnlyPredicate()
    {
        Assert.False(TrafficPredicate.Create().IsTcpOnly());
        Assert.False(TrafficPredicate.Create(services: ServiceSelector.Create()).IsTcpOnly());

        DomainInvariantException emptyEx = Assert.Throws<DomainInvariantException>(() =>
            ValidRule(
                PolicyPipelineStage.CompanyDeny,
                RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.TcpReset)));
        Assert.Contains("TCP_RESET", emptyEx.Message, StringComparison.Ordinal);

        ServiceObject tcp = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("tcp-only"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "tcp"))]);
        Dictionary<ServiceObjectId, ServiceObject> catalog = new() { [tcp.Id] = tcp };
        TrafficPredicate tcpPredicate = TrafficPredicate.Create(
            services: ServiceSelector.Create([tcp.Id]),
            serviceCatalog: catalog);
        Assert.True(tcpPredicate.IsTcpOnly());

        PolicyRule ok = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.TcpReset),
            predicate: tcpPredicate);
        Assert.Equal(RejectMode.TcpReset, ok.Effect.RejectModeValue);

        ServiceObject udp = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("udp"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Udp, "udp"))]);
        TrafficPredicate udpPredicate = TrafficPredicate.Create(
            services: ServiceSelector.Create([udp.Id]),
            serviceCatalog: new Dictionary<ServiceObjectId, ServiceObject> { [udp.Id] = udp });
        Assert.False(udpPredicate.IsTcpOnly());
        Assert.Throws<DomainInvariantException>(() =>
            ValidRule(
                PolicyPipelineStage.CompanyDeny,
                RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.TcpReset),
                predicate: udpPredicate));
    }

    [Fact]
    public void D3ExceptionEligibleDenyOnlyAndForbiddenOnMandatoryPreStateDeny()
    {
        PolicyRule eligible = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        Assert.True(eligible.ExceptionEligible);

        Assert.Throws<DomainInvariantException>(() =>
            ValidRule(
                PolicyPipelineStage.CompanyAllow,
                RuleEffectSpec.Create(PolicyRuleEffect.Accept),
                exceptionEligible: true));

        Assert.Throws<DomainInvariantException>(() =>
            ValidRule(
                PolicyPipelineStage.MandatoryPreStateDeny,
                RuleEffectSpec.Create(PolicyRuleEffect.Drop),
                exceptionEligible: true));
    }

    [Fact]
    public void D4LogPrefixBoundedAsciiNoControls()
    {
        LogSpecification ok = LogSpecification.Create(enabled: true, prefix: "mfc-ok");
        Assert.Equal("mfc-ok", ok.Prefix);

        Assert.Throws<DomainInvariantException>(() =>
            LogSpecification.Create(enabled: true, prefix: new string('a', 33)));
        Assert.Throws<DomainInvariantException>(() =>
            LogSpecification.Create(enabled: true, prefix: "bad\n"));
        Assert.Throws<DomainInvariantException>(() =>
            LogSpecification.Create(enabled: true, prefix: "ü"));
        Assert.Throws<DomainInvariantException>(() =>
            LogSpecification.Create(enabled: false, prefix: "x"));
    }

    [Fact]
    public void D5OrdinalsContiguousAndUuidIndependentOfOrdinal()
    {
        RuleId idA = RuleId.New();
        RuleId idB = RuleId.New();
        PolicyRule a = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            ordinal: 0,
            id: idA);
        PolicyRule b = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.AdminProhibited),
            ordinal: 1,
            id: idB);

        PolicyRuleSet.EnsureContiguousOrdinals([a, b], IpAddressFamily.IPv4, PolicyFilterChain.Forward, PolicyPipelineStage.CompanyDeny);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(idA, a.WithOrdinal(99).Id);

        Assert.Throws<DomainInvariantException>(() =>
            PolicyRuleSet.EnsureContiguousOrdinals(
                [a, b.WithOrdinal(2)],
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                PolicyPipelineStage.CompanyDeny));

        IReadOnlyList<PolicyRule> reordered = PolicyRuleSet.WithReorder(
            [a, b],
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            [idB, idA]);
        Assert.Equal(0u, reordered.Single(r => r.Id == idB).Ordinal);
        Assert.Equal(1u, reordered.Single(r => r.Id == idA).Ordinal);
    }

    [Fact]
    public void D6DisabledRulesExcludedFromActiveEvaluation()
    {
        PolicyRule enabled = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            enabled: true);
        PolicyRule disabled = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            ordinal: 1,
            enabled: false);

        IReadOnlyList<PolicyRule> active = PolicyRuleSet.ActiveRules([enabled, disabled]);
        Assert.Equal([enabled.Id], active.Select(static r => r.Id).ToArray());
    }

    [Fact]
    public void D7ZoneSelectorChainConstraints()
    {
        ZoneSelector zones = ZoneSelector.Create([ZoneId.New()]);

        ZoneSelector.EnsureAllowedOnChain(PolicyFilterChain.Forward, zones, zones);
        ZoneSelector.EnsureAllowedOnChain(PolicyFilterChain.Input, zones, null);
        ZoneSelector.EnsureAllowedOnChain(PolicyFilterChain.Output, null, zones);

        Assert.Throws<DomainInvariantException>(() =>
            ZoneSelector.EnsureAllowedOnChain(PolicyFilterChain.Input, zones, zones));
        Assert.Throws<DomainInvariantException>(() =>
            ZoneSelector.EnsureAllowedOnChain(PolicyFilterChain.Output, zones, zones));

        Assert.Throws<DomainInvariantException>(() =>
            PolicyRule.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Input,
                PolicyPipelineStage.CompanyDeny,
                0,
                TrafficPredicate.Create(egressZones: zones),
                RuleEffectSpec.Create(PolicyRuleEffect.Drop)));

        Assert.Throws<DomainInvariantException>(() =>
            PolicyRule.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Output,
                PolicyPipelineStage.CompanyDeny,
                0,
                TrafficPredicate.Create(ingressZones: zones),
                RuleEffectSpec.Create(PolicyRuleEffect.Drop)));
    }

    [Fact]
    public void D7ZoneSelectorRejectsDuplicateIds()
    {
        ZoneId id = ZoneId.New();
        Assert.Throws<DomainInvariantException>(() => ZoneSelector.Create([id, id]));
    }

    [Fact]
    public void D8NoRawMatcherStringOnTrafficPredicate()
    {
        foreach (PropertyInfo property in typeof(TrafficPredicate).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            Assert.False(
                property.PropertyType == typeof(string),
                $"TrafficPredicate must not expose raw matcher string '{property.Name}'.");
        }

        TrafficPredicate predicate = TrafficPredicate.Create(
            connectionStates: [ConnectionState.New, ConnectionState.Established],
            connectionNatStates: [ConnectionNatState.SrcNat],
            sourceAddressTypes: [AddressType.Unicast],
            destinationAddressTypes: [AddressType.Local],
            tcpFlags: TcpFlagConstraint.Create([TcpHeaderBit.Syn], [TcpHeaderBit.Ack]),
            ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.In, IpsecPolicyKind.Ipsec));

        Assert.Equal([ConnectionState.New, ConnectionState.Established], predicate.ConnectionStates);
        Assert.NotNull(predicate.TcpFlags);
        Assert.NotNull(predicate.IpsecPolicy);
    }

    [Fact]
    public void D9IdenticalTypedDocumentsProduceIdenticalCanonicalBytes()
    {
        RuleId id = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        PolicyDocument left = DocumentWithRule(ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            id: id));
        PolicyDocument right = DocumentWithRule(ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            id: id));

        Assert.Equal(PolicyCanonicalWriter.Write(left), PolicyCanonicalWriter.Write(right));
    }

    [Fact]
    public void D10UnsupportedMatcherSurfaceAbsentFromDomainTypes()
    {
        string[] forbidden =
        [
            "SrcMac", "PacketMark", "ConnectionMark", "RoutingMark", "Layer7",
            "TlsHost", "ConnectionRate", "Hotspot", "Dscp", "RawMatcher", "MatcherString",
        ];
        Type[] types =
        [
            typeof(TrafficPredicate),
            typeof(PolicyRule),
            typeof(RuleEffectSpec),
            typeof(ZoneSelector),
        ];
        foreach (Type type in types)
        {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static))
            {
                foreach (string name in forbidden)
                {
                    Assert.DoesNotContain(name, member.Name, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public void D11WriterReaderRoundTripPreservesContentHash()
    {
        ServiceObject tcp = ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("tcp"),
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "tcp"))]);
        TrafficPredicate predicate = TrafficPredicate.Create(
            ingressZones: ZoneSelector.Create([ZoneId.New()]),
            egressZones: ZoneSelector.Create([ZoneId.New()]),
            services: ServiceSelector.Create([tcp.Id]),
            connectionStates: [ConnectionState.Established, ConnectionState.Related],
            tcpFlags: TcpFlagConstraint.Create([TcpHeaderBit.Ack], null),
            ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.Out, IpsecPolicyKind.None),
            serviceCatalog: new Dictionary<ServiceObjectId, ServiceObject> { [tcp.Id] = tcp });

        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            predicate,
            RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.TcpReset),
            LogSpecification.Create(true, "deny"),
            enabled: true,
            exceptionEligible: true,
            description: "round-trip",
            id: new RuleId(Guid.Parse("22222222-2222-2222-2222-222222222222")));

        PolicyDocument original = DocumentWithRule(rule);
        byte[] bytes = PolicyCanonicalWriter.Write(original);
        string hash = PolicyHashing.HashContent(bytes).ToString();

        PolicyDocument parsed = PolicyDocumentReader.Read(bytes);
        byte[] again = PolicyCanonicalWriter.Write(parsed);
        Assert.Equal(bytes, again);
        Assert.Equal(hash, PolicyHashing.HashContent(again).ToString());
        Assert.Equal(rule.Id, parsed.Rules[0].Id);
        Assert.Equal(rule.Effect.Kind, parsed.Rules[0].Effect.Kind);
    }

    [Fact]
    public void D12UnsupportedRulesShapeThrowsNamedError()
    {
        PolicyDocument empty = PolicyDocument.CreateEmpty(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company);
        byte[] emptyBytes = PolicyCanonicalWriter.Write(empty);
        PolicyDocument roundTripEmpty = PolicyDocumentReader.Read(emptyBytes);
        Assert.Empty(roundTripEmpty.Rules);

        using JsonDocument doc = JsonDocument.Parse(emptyBytes);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in doc.RootElement.EnumerateObject())
            {
                if (property.NameEquals("rules"))
                {
                    writer.WritePropertyName("rules");
                    writer.WriteRawValue("""[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","raw-matcher":"src-mac-address"}]""");
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            PolicyDocumentReader.Read(stream.ToArray()));
        Assert.Contains(PolicyDocumentReader.UnsupportedRulesShapeCode, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderRoundTripsFullPredicateAndAlternateEnums()
    {
        AddressObjectId src = AddressObjectId.New();
        AddressObjectId dst = AddressObjectId.New();
        ZoneId ingress = ZoneId.New();
        ZoneId egress = ZoneId.New();
        TrafficPredicate predicate = TrafficPredicate.Create(
            sourceAddresses: AddressSelector.Create([src], [AddressObjectId.New()]),
            destinationAddresses: AddressSelector.Create([dst]),
            ingressZones: ZoneSelector.Create([ingress], [ZoneId.New()]),
            egressZones: ZoneSelector.Create([egress]),
            connectionStates: [ConnectionState.Invalid, ConnectionState.Untracked],
            connectionNatStates: [ConnectionNatState.DstNat, ConnectionNatState.SrcNat],
            sourceAddressTypes:
            [
                AddressType.Broadcast, AddressType.Multicast, AddressType.Anycast,
                AddressType.Blackhole, AddressType.Prohibit, AddressType.Unreachable,
            ],
            destinationAddressTypes: [AddressType.Unicast, AddressType.Local],
            tcpFlags: TcpFlagConstraint.Create(
                [TcpHeaderBit.Fin, TcpHeaderBit.Rst, TcpHeaderBit.Psh, TcpHeaderBit.Ece],
                [TcpHeaderBit.Urg, TcpHeaderBit.Cwr]),
            ipsecPolicy: IpsecPolicyPredicate.Create(IpsecDirection.In, IpsecPolicyKind.Ipsec));

        PolicyRule ipv6 = PolicyRule.Create(
            IpAddressFamily.IPv6,
            PolicyFilterChain.Input,
            PolicyPipelineStage.ProtectedControlPlane,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            LogSpecification.Disabled,
            id: new RuleId(Guid.Parse("33333333-3333-3333-3333-333333333333")));

        PolicyRule output = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Output,
            PolicyPipelineStage.NodeAllow,
            0,
            TrafficPredicate.Create(egressZones: ZoneSelector.Create([egress])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            LogSpecification.Create(true, "out"),
            id: new RuleId(Guid.Parse("44444444-4444-4444-4444-444444444444")));

        PolicyRule forward = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.SiteDeny,
            0,
            predicate,
            RuleEffectSpec.Create(PolicyRuleEffect.Reject, RejectMode.PortUnreachable),
            LogSpecification.Create(true, "full"),
            id: new RuleId(Guid.Parse("55555555-5555-5555-5555-555555555555")));

        PolicyDocument document = PolicyDocument.CreateEmpty(
                PolicyKind.CompanyBaseline, PolicyOwnerScope.Company)
            .WithRules([ipv6, output, forward]);
        byte[] bytes = PolicyCanonicalWriter.Write(document);
        PolicyDocument parsed = PolicyDocumentReader.Read(bytes);
        Assert.Equal(3, parsed.Rules.Count);
        Assert.Equal(IpAddressFamily.IPv6, parsed.Rules[0].Family);
        Assert.Equal(PolicyFilterChain.Input, parsed.Rules[0].Chain);
        Assert.Equal(PolicyFilterChain.Output, parsed.Rules[1].Chain);
        Assert.NotNull(parsed.Rules[2].Predicate.SourceAddresses);
        Assert.NotNull(parsed.Rules[2].Predicate.ConnectionNatStates);
        Assert.Equal(bytes, PolicyCanonicalWriter.Write(parsed));

        PolicyDocument site = PolicyDocument.CreateEmpty(PolicyKind.SiteOverlay, PolicyOwnerScope.Site);
        PolicyDocument parsedSite = PolicyDocumentReader.Read(PolicyCanonicalWriter.Write(site));
        Assert.Equal(PolicyKind.SiteOverlay, parsedSite.Kind);
        Assert.Empty(parsedSite.ChainContracts.Items);

        PolicyDocument node = PolicyDocument.CreateEmpty(PolicyKind.NodeOverlay, PolicyOwnerScope.Node);
        Assert.Equal(PolicyKind.NodeOverlay, PolicyDocumentReader.Read(PolicyCanonicalWriter.Write(node)).Kind);
    }

    [Fact]
    public void ReaderRejectsMalformedDocumentsAndUnknownEnums()
    {
        Assert.Throws<DomainInvariantException>(() => PolicyDocumentReader.Read("{}"u8.ToArray()));
        Assert.Throws<DomainInvariantException>(() => PolicyDocumentReader.Read("not-json"u8.ToArray()));

        PolicyDocument empty = PolicyDocument.CreateEmpty(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company);
        byte[] good = PolicyCanonicalWriter.Write(empty);

        DomainInvariantException schema = Assert.Throws<DomainInvariantException>(
            () => PolicyDocumentReader.Read(ReplaceJsonField(good, "schema", "\"other\"")));
        Assert.Contains("schema", schema.Message, StringComparison.OrdinalIgnoreCase);

        DomainInvariantException kind = Assert.Throws<DomainInvariantException>(
            () => PolicyDocumentReader.Read(ReplaceJsonField(good, "policy_kind", "\"UNKNOWN\"")));
        Assert.Contains("kind", kind.Message, StringComparison.OrdinalIgnoreCase);

        DomainInvariantException scope = Assert.Throws<DomainInvariantException>(
            () => PolicyDocumentReader.Read(ReplaceJsonField(good, "owner_scope", "\"UNKNOWN\"")));
        Assert.Contains("owner scope", scope.Message, StringComparison.OrdinalIgnoreCase);

        DomainInvariantException rulesShape = Assert.Throws<DomainInvariantException>(
            () => PolicyDocumentReader.Read(ReplaceJsonField(good, "rules", "{}")));
        Assert.Contains(PolicyDocumentReader.UnsupportedRulesShapeCode, rulesShape.Message, StringComparison.Ordinal);

        DomainInvariantException ruleNotObject = Assert.Throws<DomainInvariantException>(
            () => PolicyDocumentReader.Read(ReplaceJsonField(good, "rules", "[1]")));
        Assert.Contains(PolicyDocumentReader.UnsupportedRulesShapeCode, ruleNotObject.Message, StringComparison.Ordinal);

        DomainInvariantException contracts = Assert.Throws<DomainInvariantException>(
            () => PolicyDocumentReader.Read(ReplaceJsonField(good, "chain_contracts", "{}")));
        Assert.Contains("chain_contracts", contracts.Message, StringComparison.Ordinal);

        DomainInvariantException metadata = Assert.Throws<DomainInvariantException>(
            () => PolicyDocumentReader.Read(ReplaceJsonField(good, "exception_metadata", "[]")));
        Assert.Contains("exception_metadata", metadata.Message, StringComparison.Ordinal);

        PolicyDocument site = PolicyDocument.CreateEmpty(PolicyKind.SiteOverlay, PolicyOwnerScope.Site);
        byte[] siteBytes = PolicyCanonicalWriter.Write(site);
        DomainInvariantException overlayContracts = Assert.Throws<DomainInvariantException>(
            () => PolicyDocumentReader.Read(ReplaceJsonField(
                siteBytes,
                "chain_contracts",
                """[{"family":"IPv4","chain":"FORWARD","default_disposition":"DROP"}]""")));
        Assert.Contains("cannot define chain contracts", overlayContracts.Message, StringComparison.Ordinal);

        byte[] withRule = PolicyCanonicalWriter.Write(DocumentWithRule(ValidRule(
            PolicyPipelineStage.CompanyAllow,
            RuleEffectSpec.Create(PolicyRuleEffect.Accept))));
        string json = Encoding.UTF8.GetString(withRule);
        Assert.Throws<DomainInvariantException>(() =>
            PolicyDocumentReader.Read(Encoding.UTF8.GetBytes(json.Replace("\"IPv4\"", "\"IPvX\"", StringComparison.Ordinal))));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyDocumentReader.Read(Encoding.UTF8.GetBytes(json.Replace("\"FORWARD\"", "\"SIDEWAYS\"", StringComparison.Ordinal))));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyDocumentReader.Read(Encoding.UTF8.GetBytes(json.Replace("\"COMPANY_ALLOW\"", "\"NOPE\"", StringComparison.Ordinal))));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyDocumentReader.Read(Encoding.UTF8.GetBytes(json.Replace("\"ACCEPT\"", "\"YEET\"", StringComparison.Ordinal))));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyDocumentReader.Read(Encoding.UTF8.GetBytes(json.Replace("\"COMPANY\"", "\"GALAXY\"", StringComparison.Ordinal))));
    }

    [Fact]
    public void PolicyRuleSetRejectsDuplicateMissingAndInvalidReorder()
    {
        PolicyRule a = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            id: RuleId.New());
        PolicyRule b = ValidRule(
            PolicyPipelineStage.CompanyDeny,
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            ordinal: 1,
            id: RuleId.New());

        Assert.Throws<DomainInvariantException>(() => PolicyRuleSet.WithAdd([a], a));
        Assert.Throws<DomainInvariantException>(() => PolicyRuleSet.WithDelete([a], RuleId.New()));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyRuleSet.WithUpdate([a], b.WithOrdinal(0)));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyRuleSet.EnsureContiguousOrdinals([a, a]));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyRuleSet.WithReorder(
                [a, b],
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                PolicyPipelineStage.CompanyDeny,
                [a.Id]));
        Assert.Throws<DomainInvariantException>(() =>
            PolicyRuleSet.WithReorder(
                [a, b],
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                PolicyPipelineStage.CompanyDeny,
                [a.Id, a.Id]));

        PolicyRule moved = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: a.Id);
        IReadOnlyList<PolicyRule> updated = PolicyRuleSet.WithUpdate([a, b], moved);
        Assert.Contains(updated, r => r.Id == a.Id && r.Stage == PolicyPipelineStage.CompanyAllow);
        Assert.Equal(0u, updated.Single(r => r.Id == b.Id).Ordinal);
    }

    private static byte[] ReplaceJsonField(byte[] utf8, string name, string rawValue)
    {
        using JsonDocument doc = JsonDocument.Parse(utf8);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in doc.RootElement.EnumerateObject())
            {
                if (property.NameEquals(name))
                {
                    writer.WritePropertyName(name);
                    writer.WriteRawValue(rawValue);
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static PolicyRule ValidRule(
        PolicyPipelineStage stage,
        RuleEffectSpec effect,
        PolicyFilterChain chain = PolicyFilterChain.Forward,
        uint ordinal = 0,
        bool enabled = true,
        bool exceptionEligible = false,
        TrafficPredicate? predicate = null,
        RuleId? id = null)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            chain,
            stage,
            ordinal,
            predicate ?? TrafficPredicate.Create(),
            effect,
            LogSpecification.Disabled,
            enabled,
            exceptionEligible,
            description: null,
            id: id);

    private static PolicyDocument DocumentWithRule(PolicyRule rule)
        => PolicyDocument.CreateEmpty(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company)
            .WithRules([rule]);
}
