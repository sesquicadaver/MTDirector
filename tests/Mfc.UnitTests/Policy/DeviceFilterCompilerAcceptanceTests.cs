using System.Net;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

/// <summary>Living Spec AC rows for M3-08 compiler integration / Spec §33 topology vectors.</summary>
public sealed class DeviceFilterCompilerAcceptanceTests
{
    private static readonly DeviceId DeviceA = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee1"));
    private static readonly DeviceId DeviceB = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee2"));
    private static readonly Hash256 Logical = Hash256.ParseHex(
        "1111111111111111111111111111111111111111111111111111111111111111");
    private static readonly Hash256 Bundle = Hash256.ParseHex(
        "2222222222222222222222222222222222222222222222222222222222222222");
    private static readonly Hash256 Capability = Hash256.ParseHex(
        "3333333333333333333333333333333333333333333333333333333333333333");
    private static readonly DateTimeOffset CompiledAt =
        DateTimeOffset.Parse("2026-08-18T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly ZoneId MgmtZone = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
    private static readonly ZoneId WanZone = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"));
    private static readonly AddressObjectId MgmtAddressId = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"));
    private static readonly ServiceObjectId ApiSslId = new(Guid.Parse("cccccccc-0000-0000-0000-000000000001"));
    private static readonly Guid InputAllowId = Guid.Parse("11111111-2222-3333-4444-555555555501");
    private static readonly Guid DenyId = Guid.Parse("11111111-2222-3333-4444-555555555502");
    private static readonly Guid ExemptId = Guid.Parse("11111111-2222-3333-4444-555555555503");
    private static readonly Guid FastTrackId = Guid.Parse("11111111-2222-3333-4444-555555555504");
    private static readonly Guid AllowAId = Guid.Parse("11111111-2222-3333-4444-555555555505");
    private static readonly Guid AllowBId = Guid.Parse("11111111-2222-3333-4444-555555555506");

    [Fact]
    public void Ac1StandaloneIpv4InputAllow()
    {
        DeviceFilterCompileResult result = new DeviceFilterCompiler().Compile(StandaloneIpv4Request());
        Assert.True(result.IsSuccess, result.Message);
        RouterOsFilterArtifact artifact = result.Artifact!;
        Assert.Single(artifact.AddressLists);
        ChainArtifact root = Assert.Single(artifact.Chains, static c => c.Role == FilterChainArtifactRole.Root);
        Assert.Equal(FilterBuiltInContext.Input, root.BuiltInContext);
        Assert.Contains(root.Rules, static r => r.Action == "accept");
        Assert.Equal("drop", root.Rules[^1].Action);
        Assert.Equal(CompilerComments.Terminal, root.Rules[^1].Comment);
        Assert.Single(artifact.AnchorTargets);
        Assert.StartsWith("mfc4.", root.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DualStackCompilation()
    {
        DeviceFilterCompileResult result = new DeviceFilterCompiler().Compile(DualStackRequest());
        Assert.True(result.IsSuccess, result.Message);
        RouterOsFilterArtifact artifact = result.Artifact!;
        Assert.Contains(artifact.Chains, static c =>
            c.Role == FilterChainArtifactRole.Root
            && c.Family == IpAddressFamily.IPv4
            && c.Name.StartsWith("mfc4.", StringComparison.Ordinal));
        Assert.Contains(artifact.Chains, static c =>
            c.Role == FilterChainArtifactRole.Root
            && c.Family == IpAddressFamily.IPv6
            && c.Name.StartsWith("mfc6.", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac3MultiWanIndependentOfActiveRoute()
    {
        DeviceFilterCompileRequest primary = MultiWanRequest(activeWanName: "ether1");
        DeviceFilterCompileRequest backup = MultiWanRequest(activeWanName: "ether2");
        DeviceFilterCompileResult a = new DeviceFilterCompiler().Compile(primary);
        DeviceFilterCompileResult b = new DeviceFilterCompiler().Compile(backup);
        Assert.True(a.IsSuccess, a.Message);
        Assert.True(b.IsSuccess, b.Message);
        Assert.Equal(a.Artifact!.ResourceHash.ToString(), b.Artifact!.ResourceHash.ToString());
        Assert.Equal(
            a.Provenance!.DeviceResolvedPolicyHash.ToString(),
            b.Provenance!.DeviceResolvedPolicyHash.ToString());
        IEnumerable<string> interfaces = a.Artifact.Chains
            .SelectMany(static c => c.Rules)
            .SelectMany(static r => r.Matchers)
            .Where(static kv => kv.Key is "in-interface" or "out-interface")
            .Select(static kv => kv.Value);
        Assert.Contains("ether1", interfaces);
        Assert.Contains("ether2", interfaces);
    }

    [Fact]
    public void Ac4VrrpMembersShareLogicalHash()
    {
        NodeFilterCompileResult node = new DeviceFilterCompiler().CompileNode(
        [
            VrrpMemberRequest(DeviceA, "ether5"),
            VrrpMemberRequest(DeviceB, "bridge-mgmt"),
        ]);
        Assert.True(node.IsSuccess, node.Message);
        Assert.Equal(Logical.ToString(), node.LogicalEffectivePolicyHash!.ToString());
        Assert.Equal(2, node.Devices.Count);
        Assert.NotEqual(
            node.Devices[0].Provenance!.DeviceResolvedPolicyHash.ToString(),
            node.Devices[1].Provenance!.DeviceResolvedPolicyHash.ToString());
        Assert.NotEqual(
            node.Devices[0].Artifact!.ResourceHash.ToString(),
            node.Devices[1].Artifact!.ResourceHash.ToString());
        Guid[] leftIds = node.Devices[0].Artifact!.Chains
            .SelectMany(static c => c.Rules)
            .Where(static r => r.LogicalRuleId is not null)
            .Select(static r => r.LogicalRuleId!.Value)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();
        Guid[] rightIds = node.Devices[1].Artifact!.Chains
            .SelectMany(static c => c.Rules)
            .Where(static r => r.LogicalRuleId is not null)
            .Select(static r => r.LogicalRuleId!.Value)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();
        Assert.Equal(leftIds, rightIds);
    }

    [Fact]
    public void Ac5SplitMasterRoleIsNotAnInput()
    {
        // DeviceFilterCompileRequest has no VRRP role field; identical zone inputs stay identical.
        DeviceFilterCompileResult first = new DeviceFilterCompiler().Compile(StandaloneIpv4Request());
        DeviceFilterCompileResult second = new DeviceFilterCompiler().Compile(StandaloneIpv4Request());
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Artifact!.ResourceHash.ToString(), second.Artifact!.ResourceHash.ToString());
        Assert.Null(typeof(DeviceFilterCompileRequest).GetProperty("VrrpRole"));
        Assert.Null(typeof(DeviceFilterCompileRequest).GetProperty("SplitMasterRole"));
    }

    [Fact]
    public void Ac6SwitchForwardCompilationIsForbidden()
    {
        DeviceFilterCompileRequest switchForward = BaseRequest(
            DeviceA,
            contracts: ForwardContracts(IpAddressFamily.IPv4),
            rules: [AcceptForward(AllowAId)],
            nodeKind: NodeKind.Switch);
        DeviceFilterCompileResult forbidden = new DeviceFilterCompiler().Compile(switchForward);
        Assert.False(forbidden.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.SwitchForwardCompilationForbidden, forbidden.Code);
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(forbidden.Code!));

        DeviceFilterCompileRequest switchInput = BaseRequest(
            DeviceA,
            contracts: InputContracts(IpAddressFamily.IPv4),
            rules: [StandaloneInputAllow()],
            addresses: MgmtCatalog(),
            services: ApiSslCatalog(),
            binding: Binding(MgmtZone, NodeZoneBindingKind.SingleInterface, ["ether1"], ["ether1"]),
            observation: Observation(["ether1"]),
            nodeKind: NodeKind.Switch);
        DeviceFilterCompileResult allowed = new DeviceFilterCompiler().Compile(switchInput);
        Assert.True(allowed.IsSuccess, allowed.Message);
    }

    [Fact]
    public void Ac7SameAddressContentIsDeduplicated()
    {
        AddressInterval content = AddressInterval.FromPrefix(
            IpAddressFamily.IPv4,
            IPAddress.Parse("10.0.0.0"),
            24);
        AddressObject leftObj = AddressObject.Reconstitute(
            new AddressObjectId(Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000a1")),
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("hosts-a"),
            IpAddressFamily.IPv4,
            null,
            [content]);
        AddressObject rightObj = AddressObject.Reconstitute(
            new AddressObjectId(Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000a2")),
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("hosts-b"),
            IpAddressFamily.IPv4,
            null,
            [content]);
        Dictionary<AddressObjectId, AddressObject> addresses = new()
        {
            [leftObj.Id] = leftObj,
            [rightObj.Id] = rightObj,
        };
        PolicyRule left = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([leftObj.Id])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: new RuleId(AllowAId));
        PolicyRule right = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 1,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([rightObj.Id])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: new RuleId(AllowBId));
        DeviceFilterCompileResult result = new DeviceFilterCompiler().Compile(BaseRequest(
            DeviceA,
            contracts: ForwardContracts(IpAddressFamily.IPv4),
            rules: [left, right],
            addresses: addresses));
        Assert.True(result.IsSuccess, result.Message);
        Assert.NotEqual(leftObj.Id, rightObj.Id);
        Assert.Single(result.Artifact!.AddressLists);
    }

    [Fact]
    public void Ac8ExceptionChainLayoutIsCorrect()
    {
        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage),
            id: new RuleId(ExemptId));
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            id: new RuleId(DenyId));
        DeviceFilterCompileResult result = new DeviceFilterCompiler().Compile(BaseRequest(
            DeviceA,
            contracts: ForwardContracts(IpAddressFamily.IPv4),
            rules: [exempt, deny]));
        Assert.True(result.IsSuccess, result.Message);
        ChainArtifact denyChain = Assert.Single(
            result.Artifact!.Chains,
            static c => c.Role == FilterChainArtifactRole.CompanyDeny);
        Assert.Equal(3, denyChain.Rules.Length);
        Assert.Equal("return", denyChain.Rules[0].Action);
        Assert.EndsWith(":ex", denyChain.Rules[0].Comment, StringComparison.Ordinal);
        Assert.Equal("drop", denyChain.Rules[1].Action);
        Assert.Equal("return", denyChain.Rules[2].Action);
        Assert.Equal(CompilerComments.ReturnCompanyDeny, denyChain.Rules[2].Comment);

        ChainArtifact root = Assert.Single(result.Artifact.Chains, static c => c.Role == FilterChainArtifactRole.Root);
        Assert.Contains(root.Rules, static r => r.Action == "jump");
        Assert.Equal("drop", root.Rules[^1].Action);
    }

    [Fact]
    public void Ac9FastTrackPairIsCorrect()
    {
        ServiceObject tcp = ServiceObject.Reconstitute(
            ApiSslId,
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("tcp-ft"),
            null,
            [ServiceTerm.Create(IpProtocol.Create(IpProtocol.Tcp, "tcp"))]);
        Dictionary<ServiceObjectId, ServiceObject> services = new() { [tcp.Id] = tcp };
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.StatePrelude,
            ordinal: 0,
            TrafficPredicate.Create(
                services: ServiceSelector.Create([tcp.Id]),
                connectionStates: [ConnectionState.Established, ConnectionState.Related],
                serviceCatalog: services),
            RuleEffectSpec.Create(PolicyRuleEffect.FasttrackAccept),
            id: new RuleId(FastTrackId));
        DeviceFilterCompileResult result = new DeviceFilterCompiler().Compile(BaseRequest(
            DeviceA,
            contracts: ForwardContracts(IpAddressFamily.IPv4),
            rules: [rule],
            services: services,
            fastTrack: FastTrackTopologyContext.SafeSingleWan));
        Assert.True(result.IsSuccess, result.Message);
        ChainArtifact root = Assert.Single(result.Artifact!.Chains, static c => c.Role == FilterChainArtifactRole.Root);
        FilterRuleArtifact[] pair = root.Rules
            .Where(static r => r.LogicalRuleId == FastTrackId)
            .OrderBy(static r => r.Ordinal)
            .ToArray();
        Assert.Equal(2, pair.Length);
        Assert.Equal("fasttrack-connection", pair[0].Action);
        Assert.Equal("accept", pair[1].Action);
        Assert.Equal(pair[0].Matchers, pair[1].Matchers);
        Assert.Equal(pair[0].Ordinal + 1, pair[1].Ordinal);
        Assert.EndsWith(":ft", pair[0].Comment, StringComparison.Ordinal);
        Assert.EndsWith(":ac", pair[1].Comment, StringComparison.Ordinal);
        Assert.Equal("no", pair[0].ActionParameters["hw-offload"]);
    }

    [Fact]
    public void Ac10RootAndDenyTerminalsPresent()
    {
        DeviceFilterCompileResult result = new DeviceFilterCompiler().Compile(BaseRequest(
            DeviceA,
            contracts: ForwardContracts(IpAddressFamily.IPv4),
            rules:
            [
                PolicyRule.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    PolicyPipelineStage.CompanyDeny,
                    ordinal: 0,
                    TrafficPredicate.Create(),
                    RuleEffectSpec.Create(PolicyRuleEffect.Drop),
                    id: new RuleId(DenyId)),
            ]));
        Assert.True(result.IsSuccess, result.Message);
        ChainArtifact root = Assert.Single(result.Artifact!.Chains, static c => c.Role == FilterChainArtifactRole.Root);
        Assert.Equal(CompilerComments.Terminal, root.Rules[^1].Comment);
        Assert.Equal("drop", root.Rules[^1].Action);
        ChainArtifact deny = Assert.Single(
            result.Artifact.Chains,
            static c => c.Role == FilterChainArtifactRole.CompanyDeny);
        Assert.Equal(CompilerComments.ReturnCompanyDeny, deny.Rules[^1].Comment);
        Assert.Equal("return", deny.Rules[^1].Action);
    }

    [Fact]
    public void Ac11DescriptionOnlyChangeDoesNotAlterResourceHash()
    {
        PolicyRule plain = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            description: "alpha",
            id: new RuleId(AllowAId));
        PolicyRule renamed = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            description: "beta-only-description",
            id: new RuleId(AllowAId));
        DeviceFilterCompileResult a = new DeviceFilterCompiler().Compile(BaseRequest(
            DeviceA,
            contracts: ForwardContracts(IpAddressFamily.IPv4),
            rules: [plain]));
        DeviceFilterCompileResult b = new DeviceFilterCompiler().Compile(BaseRequest(
            DeviceA,
            contracts: ForwardContracts(IpAddressFamily.IPv4),
            rules: [renamed]));
        Assert.True(a.IsSuccess, a.Message);
        Assert.True(b.IsSuccess, b.Message);
        Assert.Equal(a.Artifact!.ResourceHash.ToString(), b.Artifact!.ResourceHash.ToString());
        Assert.Equal(a.Artifact.PhysicalSemanticsHash.ToString(), b.Artifact.PhysicalSemanticsHash.ToString());
    }

    [Fact]
    public void Ac12CompileIsDeterministic()
    {
        DeviceFilterCompileResult first = new DeviceFilterCompiler().Compile(StandaloneIpv4Request());
        DeviceFilterCompileResult second = new DeviceFilterCompiler().Compile(StandaloneIpv4Request());
        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(first.Artifact!.ResourceHash.ToString(), second.Artifact!.ResourceHash.ToString());
        Assert.True(first.Artifact.CanonicalBytes.AsSpan().SequenceEqual(second.Artifact.CanonicalBytes.AsSpan()));
        Assert.Equal(first.Artifact.ArtifactId, second.Artifact.ArtifactId);
    }

    private static DeviceFilterCompileRequest StandaloneIpv4Request()
        => BaseRequest(
            DeviceA,
            contracts: InputContracts(IpAddressFamily.IPv4),
            rules: [StandaloneInputAllow()],
            addresses: MgmtCatalog(),
            services: ApiSslCatalog(),
            binding: Binding(MgmtZone, NodeZoneBindingKind.SingleInterface, ["ether1"], ["ether1"]),
            observation: Observation(["ether1"]));

    private static DeviceFilterCompileRequest DualStackRequest()
    {
        ChainContractSet contracts = ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.Drop,
                    rejectMode: null,
                    PolicyRuntimeMode.ManagedOnly),
                ChainContract.Create(
                    IpAddressFamily.IPv6,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.Drop,
                    rejectMode: null,
                    PolicyRuntimeMode.ManagedOnly),
            ],
            PolicyRuntimeMode.ManagedOnly);
        PolicyRule v4 = AcceptForward(AllowAId, IpAddressFamily.IPv4);
        PolicyRule v6 = AcceptForward(AllowBId, IpAddressFamily.IPv6);
        return BaseRequest(DeviceA, contracts, [v4, v6]);
    }

    private static DeviceFilterCompileRequest MultiWanRequest(string? activeWanName)
    {
        NodeZoneBinding binding = Binding(
            WanZone,
            NodeZoneBindingKind.ExplicitInterfaceSet,
            ["ether1", "ether2"],
            ["ether1", "ether2"]);
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(egressZones: ZoneSelector.Create([WanZone])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: new RuleId(AllowAId));
        return BaseRequest(
            DeviceA,
            contracts: ForwardContracts(IpAddressFamily.IPv4),
            rules: [rule],
            binding: binding,
            observation: Observation(["ether1", "ether2"]),
            activeWanName: activeWanName);
    }

    private static DeviceFilterCompileRequest VrrpMemberRequest(DeviceId deviceId, string mgmtInterface)
    {
        NodeZoneBinding binding = Binding(
            MgmtZone,
            NodeZoneBindingKind.SingleInterface,
            [mgmtInterface],
            [mgmtInterface]);
        PolicyRule rule = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Input,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(ingressZones: ZoneSelector.Create([MgmtZone])),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: new RuleId(InputAllowId));
        return BaseRequest(
            deviceId,
            contracts: InputContracts(IpAddressFamily.IPv4),
            rules: [rule],
            binding: binding,
            observation: Observation([mgmtInterface]));
    }

    private static PolicyRule StandaloneInputAllow()
    {
        AddressObject mgmt = MgmtCatalog()[MgmtAddressId];
        ServiceObject api = ApiSslCatalog()[ApiSslId];
        return PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Input,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([mgmt.Id]),
                services: ServiceSelector.Create([api.Id]),
                serviceCatalog: ApiSslCatalog()),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: new RuleId(InputAllowId));
    }

    private static PolicyRule AcceptForward(Guid id, IpAddressFamily family = IpAddressFamily.IPv4)
        => PolicyRule.Create(
            family,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept),
            id: new RuleId(id));

    private static DeviceFilterCompileRequest BaseRequest(
        DeviceId deviceId,
        ChainContractSet contracts,
        IReadOnlyList<PolicyRule> rules,
        IReadOnlyDictionary<AddressObjectId, AddressObject>? addresses = null,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? services = null,
        NodeZoneBinding? binding = null,
        ZoneResolveDeviceObservation? observation = null,
        string? activeWanName = null,
        FastTrackTopologyContext? fastTrack = null,
        NodeKind nodeKind = NodeKind.Router)
    {
        Dictionary<ZoneId, NodeZoneBinding> bindings = [];
        if (binding is not null)
        {
            bindings[binding.ZoneId] = binding;
        }

        return new DeviceFilterCompileRequest
        {
            DeviceId = deviceId,
            LogicalEffectivePolicyHash = Logical,
            AnalysisBundleHash = Bundle,
            CapabilityHash = Capability,
            CompilerProfileHash = RouterOsCompilerProfile.LayoutV1Hash,
            AnalysisPassed = true,
            InputApproved = true,
            AnalysisContextCurrent = true,
            CapabilityCurrent = true,
            CompilerProfileSupported = true,
            NodeKind = nodeKind,
            ActiveRules = rules,
            ChainContracts = contracts,
            Addresses = addresses ?? new Dictionary<AddressObjectId, AddressObject>(),
            Services = services ?? new Dictionary<ServiceObjectId, ServiceObject>(),
            Zones = new ZoneServiceCompileContext
            {
                DeviceId = deviceId,
                Bindings = bindings,
                Observation = observation ?? Observation(["ether1"]),
                Services = services ?? new Dictionary<ServiceObjectId, ServiceObject>(),
                ActiveWanName = activeWanName,
            },
            FastTrackTopology = fastTrack,
            CompiledAtUtc = CompiledAt,
        };
    }

    private static ChainContractSet InputContracts(IpAddressFamily family)
        => ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    family,
                    PolicyFilterChain.Input,
                    ChainDefaultDisposition.Drop,
                    rejectMode: null,
                    PolicyRuntimeMode.ManagedOnly),
            ],
            PolicyRuntimeMode.ManagedOnly);

    private static ChainContractSet ForwardContracts(IpAddressFamily family)
        => ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    family,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.Drop,
                    rejectMode: null,
                    PolicyRuntimeMode.ManagedOnly),
            ],
            PolicyRuntimeMode.ManagedOnly);

    private static Dictionary<AddressObjectId, AddressObject> MgmtCatalog()
    {
        AddressObject mgmt = AddressObject.Reconstitute(
            MgmtAddressId,
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("mgmt"),
            IpAddressFamily.IPv4,
            null,
            [AddressInterval.FromPrefix(IpAddressFamily.IPv4, IPAddress.Parse("192.0.2.0"), 24)]);
        return new Dictionary<AddressObjectId, AddressObject> { [mgmt.Id] = mgmt };
    }

    private static Dictionary<ServiceObjectId, ServiceObject> ApiSslCatalog()
    {
        ServiceObject api = ServiceObject.Reconstitute(
            ApiSslId,
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create("api-ssl"),
            null,
            [
                ServiceTerm.Create(
                    IpProtocol.Create(IpProtocol.Tcp, "tcp"),
                    destinationPorts: PortSet.Create([new PortInterval(8729, 8729)])),
            ]);
        return new Dictionary<ServiceObjectId, ServiceObject> { [api.Id] = api };
    }

    private static NodeZoneBinding Binding(
        ZoneId zoneId,
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        IReadOnlyList<string> resolvedMembers)
    {
        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(kind, values, resolvedMembers);
        return NodeZoneBinding.Create(
            new NodeId(Guid.Parse("dddddddd-0000-0000-0000-000000000001")),
            zoneId,
            kind,
            values,
            expected);
    }

    private static ZoneResolveDeviceObservation Observation(IReadOnlyList<string> interfaceNames)
        => new()
        {
            DeviceId = DeviceA,
            ObservationAvailable = true,
            Interfaces = interfaceNames
                .Select(static n => new ZoneResolveInterfaceObservation { Name = n, Dynamic = false })
                .ToArray(),
            InterfaceLists = [],
            InterfaceListMembers = [],
        };
}
